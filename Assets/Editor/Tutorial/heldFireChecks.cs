using System;
public static class heldFireChecks
{
    public static string run()
    {
        int n=0;
        Action<bool,string> check=(ok,label)=>{ if(!ok) throw new Exception(label); n++; };
        var s=new heldFireState();
        check(!s.tick(0,.2f,true,true,false),"no lane"); s.select(0);
        check(!s.tick(0,.2f,true,true,false),"fresh F required");
        s.tick(0,.2f,false,true,false);
        check(s.tick(0,.2f,true,true,false),"immediate first shot");
        check(!s.tick(.1,.2f,true,true,false),"cooldown");
        s.tick(.12,.2f,false,true,false); s.select(3);
        check(!s.tick(.15,.2f,true,true,false),"lane/repress cannot bypass cooldown");
        check(s.tick(.21,.2f,true,true,false),"repeat");
        check(!s.tick(.5,.2f,true,false,false),"lane release stops fire");
        check(s.tick(2,.2f,true,true,false),"slow frame one shot");
        check(!s.tick(2,.2f,true,true,false),"no catch up");
        s.tick(2,.2f,true,true,true);
        check(!s.tick(3,.2f,true,true,false),"resume latch");
        s.tick(3,.2f,false,true,false);check(s.tick(3,.2f,true,true,false),"resume after release");
        s.reset(false);check(!s.tick(4,.2f,true,true,false),"new round/replay latch");
        s.reset(true);check(s.lane==-1,"lesson/layout clears selection");
        s.select(2);s.tick(5,0,false,true,false);check(s.tick(5,0,true,true,false),"minimum interval first");
        check(!s.tick(5.01,0,true,true,false),"minimum interval");
        check(controlLayout.key(0,false)==UnityEngine.InputSystem.Key.P && controlLayout.key(3,true)==UnityEngine.InputSystem.Key.Digit4,"shared mappings");
        return "PASS: "+n+" held-fire scheduler assertions";
    }
}
