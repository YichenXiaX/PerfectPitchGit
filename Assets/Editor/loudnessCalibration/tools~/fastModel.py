import os
os.environ.setdefault("MPLCONFIGDIR",os.path.join(os.path.dirname(__file__),".cache","matplotlib"))
"""Exact scalar transcription of MoSQITo 1.2.1 nonlinear-decay recurrence.
Upstream: a14daeafcf0a37f36e4a29314ea42bc68b304c0e, Apache-2.0.
No fast-math. Retains the upstream interpolation order, threshold, wraparound
initial output state, capacitor updates and one-in-24 output sampling.
Only loop execution/storage changes; the remaining loudness stages are upstream.
"""
# SPDX-License-Identifier: Apache-2.0
# Modified MoSQITo numeric loops; attribution and changes in THIRD_PARTY_NOTICES.txt.
from importlib.metadata import version
if version("mosqito")!="1.2.1":
    raise RuntimeError("This validated calibration tool requires MoSQITo 1.2.1.")
from math import sqrt,exp
import numpy as np
from numba import njit
from mosqito.sq_metrics.loudness.loudness_zwst._main_loudness import _main_loudness
from mosqito.sq_metrics.loudness.loudness_zwst._calc_slopes import _calc_slopes
from mosqito.sq_metrics.loudness.loudness_zwtv._temporal_weighting import _temporal_weighting

dt=1/(2000*24)
tShort=.005;tLong=.015;tVar=.075
p=(tVar+tLong)/(tVar*tShort);q=1/(tShort*tVar)
l1=-p/2+sqrt(p*p/4-q);l2=-p/2-sqrt(p*p/4-q)
den=tVar*(l1-l2);e1=exp(l1*dt);e2=exp(l2*dt)
coefficients=np.array([(e1-e2)/den,
 ((tVar*l2+1)*e1-(tVar*l1+1)*e2)/den,
 ((tVar*l1+1)*e1-(tVar*l2+1)*e2)/den,
 (tVar*l1+1)*(tVar*l2+1)*(e1-e2)/den,
 exp(-dt/tLong),exp(-dt/tVar)],dtype=np.float64)

@njit(cache=True)
def nonlinearDecayKernel(core,coef):
    bands,times=core.shape
    result=np.empty_like(core)
    for band in range(bands):
        previousOutput=core[band,times-1]
        lastDelta=(0.-previousOutput)/24
        for sub in range(23):
            previousOutput+=lastDelta
        previousCapacitor=0.
        for t in range(times):
            current=core[band,t]
            nextValue=core[band,t+1] if t+1<times else 0.
            delta=(nextValue-current)/24
            for sub in range(24):
                output=current
                output2=previousOutput*coef[2]-previousCapacitor*coef[3]
                if previousOutput>previousCapacitor and output2>=current:
                    output=output2
                output2=previousOutput*coef[4]
                if previousOutput<=previousCapacitor and output2>=current:
                    output=output2
                capacitor=output
                cap2=previousOutput*coef[0]-previousCapacitor*coef[1]
                if current<previousOutput and previousOutput>previousCapacitor and cap2<=output:
                    capacitor=cap2
                cap2=(previousCapacitor-current)*coef[5]+current
                if current>=previousOutput and not(abs(current-previousOutput)<1e-5 and output<=previousCapacitor):
                    capacitor=cap2
                if sub==0:
                    result[band,t]=output
                previousOutput=output
                previousCapacitor=capacitor
                current+=delta
    return result

def nonlinearDecay(core):
    return nonlinearDecayKernel(np.ascontiguousarray(core),coefficients)

def fastCached(cache,gain=1.):
    histories=[]
    for bandpower in cache:
        levels=10*np.log10((bandpower*gain*gain+1e-12)/4e-10)
        core=_main_loudness(levels,"free")
        core=nonlinearDecay(core)
        overall,_=_calc_slopes(core)
        histories.append(_temporal_weighting(overall)[::4])
    return np.stack(histories,axis=1)


# Scalarize the upstream vectorized specific-loudness integration, independently
# for each time column. Constants are extracted unchanged from the pinned package.
import ast,inspect
def extractSlopeConstants():
    tree=ast.parse(inspect.getsource(_calc_slopes))
    found={}
    for node in tree.body[0].body:
        if isinstance(node,ast.Assign) and len(node.targets)==1 and isinstance(node.targets[0],ast.Name):
            name=node.targets[0].id
            if name in ("zup","rns","usl"):
                found[name]=np.asarray(ast.literal_eval(node.value.args[0]),dtype=np.float64)
    return found["zup"],found["rns"],found["usl"]
zup,rns,usl=extractSlopeConstants()

@njit(cache=True)
def rnsIndex(value,rns,equalToo=False):
    value=np.round(value,8)
    result=0
    for k in range(len(rns)):
        bound=np.round(rns[k],8)
        if (value<=bound if equalToo else value<bound):
            result+=1
    return min(result,17)

@njit(cache=True)
def slopesKernel(core,zup,rns,usl):
    nt=core.shape[1]
    total=np.zeros(nt)
    specific=np.zeros((240,nt))
    edges=(zup*10).astype(np.int32)
    for t in range(nt):
        n2=np.ones(240);z2=np.ones(240);slopes=np.ones(240);dz=np.ones(240);rvalues=np.ones(240)
        for i in range(21):
            start=0 if i==0 else edges[i-1]
            end=edges[i]
            r=rnsIndex(core[i,t],rns)
            width=zup[1] if i<=1 else zup[i]-zup[i-1]
            for j in range(start,end):
                n2[j]=core[i,t];z2[j]=zup[i];dz[j]=width;slopes[j]=usl[r,min(i,7)];rvalues[j]=rns[r]
                if i<20:specific[j,t]=core[i,t]
        n1=0.;z1=0.;value=0.
        for i in range(21):
            start=0 if i==0 else edges[i-1]
            end=edges[i]
            j=start
            r=rnsIndex(n2[j-1],rns)
            rvalues[j]=rns[r]
            slopeCol=7 if i==0 else min(i-1,7)
            slopes[j]=usl[r,slopeCol]
            bigger=np.round(n2[j-1],8)>np.round(core[i,t],8)
            if not bigger:
                value+=n2[j]*(z2[j]-z1)
                n1=n2[j];z1=z2[j]
            else:
                maxRns=max(rvalues[j-1],core[i,t])
                z2[j]=min((n1-maxRns)/slopes[j]+z1,zup[i])
                dz[j]=z2[j]-z1
                n2[j]=n1-dz[j]*slopes[j]
                value+=dz[j]*(n1+n2[j])/2
                z=(zup[20] if i==0 else zup[i-1])+.1
                maskZ=False
                for j in range(start,end):
                    if j!=start:
                        z2[j]=z2[j-1];n2[j]=n2[j-1];dz[j]=dz[j-1];slopes[j]=slopes[j-1];rvalues[j]=rvalues[j-1]
                    maskZ=bigger and np.round(z2[j],8)<=np.round(z,8)
                    if maskZ:
                        r=rnsIndex(n2[j],rns,True)
                        rvalues[j]=rns[r];slopes[j]=usl[r,slopeCol]
                        n1=n2[j];z1=z2[j]
                        mask1=np.round(n1,8)<=np.round(core[i,t],8)
                        if mask1:
                            n2[j]=core[i,t];z2[j]=zup[i];dz[j]=z2[j]-z1
                            value+=n2[j]*(z2[j]-z1)
                        else:
                            maxRns=max(rvalues[j],core[i,t])
                            z2[j]=min((n1-maxRns)/slopes[j]+z1,zup[i])
                            dz[j]=z2[j]-z1
                            n2[j]=n1-dz[j]*slopes[j]
                            value+=dz[j]*(n1+n2[j])/2
                            specific[j,t]=n1-(z-z1)*slopes[j]
                        z+=.1
                        bigger=not mask1
                    else:
                        specific[j,t]=n1-(z-z1)*slopes[j]
                        z+=.1
                    z1=z2[j];n1=n2[j]
                    if not bigger:break
                z1=z2[end-1];n1=n2[end-1]
                if maskZ:
                    r=rnsIndex(n2[j],rns,True)
                    rvalues[j]=rns[r];slopes[j]=usl[r,slopeCol]
        value=max(value,0.)
        total[t]=mathFloor(value*1000+.5)/1000 if value<=16 else mathFloor(value*100+.5)/100
    return total,specific

from math import floor as mathFloor
def slopesFast(core):
    return slopesKernel(np.ascontiguousarray(core),zup,rns,usl)

def fastestCached(cache,gain=1.):
    histories=[]
    for bandpower in cache:
        levels=10*np.log10((bandpower*gain*gain+1e-12)/4e-10)
        core=_main_loudness(levels,"free")
        core=nonlinearDecay(core)
        overall,_=slopesFast(core)
        histories.append(_temporal_weighting(overall)[::4])
    return np.stack(histories,axis=1)

