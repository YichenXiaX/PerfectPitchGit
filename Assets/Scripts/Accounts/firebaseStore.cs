using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

// A small REST adapter for the existing save queue. Firestore Rules remain the
// authority for ownership, consent, record shape, and immutable research data.
public partial class cloudAccount
{
#if UNITY_EDITOR
    public IEnumerator firebaseTestApi(string path,string json,Action<long,string> done)
    {
        if(!firebaseEmulator)throw new InvalidOperationException("Integration checks are restricted to local emulators.");
        yield return api(path,"PUT",json,r=>done(r.status,r.body));
    }
#endif
    private string documentRoot => "projects/"+firebaseProject+"/databases/(default)/documents";
    private string playerPath => "players/"+userId;
    private string firestoreUrl {
        get {
#if UNITY_EDITOR || COMET_FIREBASE_TEST
            if(firebaseEmulator)return "http://127.0.0.1:8080/v1/"+documentRoot;
#endif
            return firebase.databaseUrl;
        }
    }
    private static JToken encodeField(JToken value)
    {
        if(value==null || value.Type==JTokenType.Null)return new JObject{{"nullValue",null}};
        if(value.Type==JTokenType.Object)return new JObject{{"mapValue",new JObject{{"fields",encodeFields((JObject)value)}}}};
        if(value.Type==JTokenType.Array)return new JObject{{"arrayValue",new JObject{{"values",new JArray(((JArray)value).Select(encodeField))}}}};
        string type=value.Type==JTokenType.Boolean?"booleanValue":value.Type==JTokenType.Integer?"integerValue":value.Type==JTokenType.Float?"doubleValue":"stringValue";
        return new JObject{{type,type=="integerValue"?new JValue((string)value):value.DeepClone()}};
    }
    private static JObject encodeFields(JObject value) => new JObject(value.Properties().Select(p=>new JProperty(p.Name,encodeField(p.Value))));
    private static JToken decodeField(JToken value)
    {
        if(value["integerValue"]!=null)return new JValue((long)value["integerValue"]);
        if(value["mapValue"]!=null)return decodeFields(value["mapValue"]?["fields"] as JObject ?? new JObject());
        if(value["arrayValue"]!=null)return new JArray((value["arrayValue"]?["values"] as JArray ?? new JArray()).Select(decodeField));
        return ((JObject)value).Properties().FirstOrDefault()?.Value.DeepClone() ?? JValue.CreateNull();
    }
    private static JObject decodeFields(JObject fields) => new JObject(fields.Properties().Select(p=>new JProperty(p.Name,decodeField(p.Value))));
    private static JObject documentFields(response result) => decodeFields(jsonObject(result.body)["fields"] as JObject ?? new JObject());
    private static response storeSuccess(JObject body=null) => new response{status=200,body=(body??new JObject()).ToString()};
    private static response storeConflict() => new response{status=409,body="{\"error\":\"save_conflict\"}"};
    private IEnumerator firestoreRequest(string path,string method,JObject body,Action<response> done)
    {
        response result=null;
        yield return request(firestoreUrl+path,method,body?.ToString(),accessToken,r=>result=r);
        if(result.status==401){yield return refreshFirebase();if(signedIn)yield return request(firestoreUrl+path,method,body?.ToString(),accessToken,r=>result=r);}
        done(result);
    }
    private IEnumerator readDocument(string path,Action<response> done) => firestoreRequest("/"+path,"GET",null,done);
    private IEnumerator commitDocuments(JArray writes,Action<response> done) => firestoreRequest(":commit","POST",new JObject{{"writes",writes}},done);
    private JObject documentWrite(string path,JObject fields,bool create=false,string updateTime=null,bool patch=false,params string[] timestamps)
    {
        var write=new JObject{{"update",new JObject{{"name",documentRoot+"/"+path},{"fields",encodeFields(fields)}}}};
        if(patch)write["updateMask"]=new JObject{{"fieldPaths",new JArray(fields.Properties().Select(p=>p.Name))}};
        if(create)write["currentDocument"]=new JObject{{"exists",false}};
        else if(updateTime!=null)write["currentDocument"]=new JObject{{"updateTime",updateTime}};
        if(timestamps.Length>0)write["updateTransforms"]=new JArray(timestamps.Select(p=>new JObject{{"fieldPath",p},{"setToServerValue","REQUEST_TIME"}}));
        return write;
    }
    private static bool sameValue(JToken a,JToken b)
    {
        if(a==null || b==null)return a==b;
        bool aNumber=a.Type==JTokenType.Float || a.Type==JTokenType.Integer,bNumber=b.Type==JTokenType.Float || b.Type==JTokenType.Integer;
        if(aNumber && bNumber)return (double)a==(double)b;
        if(a is JObject left && b is JObject right)return left.Count==right.Count && sameFields(left,right);
        return JToken.DeepEquals(a,b);
    }
    private static bool sameFields(JObject actual,JObject expected) => expected.Properties().All(p=>sameValue(actual[p.Name],p.Value));
    private IEnumerator firebaseApi(string path,string method,string json,Action<response> done)
    {
        switch(path) {
            case "profile": yield return firebaseProfile(done);break;
            case "progress": yield return firebaseProgress(done);break;
            case "consent": yield return firebaseConsent(jsonObject(json),done);break;
            case "tutorial": yield return firebaseTutorial(done);break;
            case "sessions": yield return firebaseStart(jsonObject(json),done);break;
            case "checkpoints": yield return firebaseCheckpoint(jsonObject(json),done);break;
            case "trials": yield return firebaseTrial(jsonObject(json),done);break;
            default: done(new response{status=400});break;
        }
    }
    private IEnumerator firebaseProfile(Action<response> done)
    {
        response result=null;yield return readDocument(playerPath,r=>result=r);
        if(result.status==404) {
            var first=new JObject{{"schemaVersion",1},{"nickname","player_"+userId.Substring(0,Math.Min(12,userId.Length))},{"consentReceiptId",""},{"tutorialVersion",0},{"tutorialCompletedAt",null}};
            yield return commitDocuments(new JArray(documentWrite(playerPath,first,true,null,false,"createdAt","updatedAt")),r=>result=r);
            if(!result.ok && result.status!=409){done(result);yield break;}
            yield return readDocument(playerPath,r=>result=r);
        }
        if(!result.ok){done(result);yield break;}
        var p=documentFields(result);
        var output=new JObject{{"nickname",p["nickname"]},{"tutorialVersion",p["tutorialVersion"]},{"consentReceiptId",p["consentReceiptId"]},{"consentStatus","notObtained"},{"consentVersion",""},{"researchCollectionEnabled",false}};
        string receiptId=(string)p["consentReceiptId"];
        if(!string.IsNullOrEmpty(receiptId)) {
            yield return readDocument(playerPath+"/consents/"+receiptId,r=>result=r);
            if(!result.ok){done(result);yield break;}
            var c=documentFields(result);output["consentStatus"]=c["decision"];output["consentVersion"]=c["formVersion"];
            output["researchCollectionEnabled"]=(string)c["formVersion"]==researchConsentState.formVersion && ((string)c["decision"]=="adultAgreed" || (string)c["decision"]=="childAndParentAgreed");
        }
        done(storeSuccess(output));
    }
    private IEnumerator firebaseProgress(Action<response> done)
    {
        var total=new progressRecord();string page="";
        do {
            response result=null;yield return firestoreRequest("/"+playerPath+"/sessions?pageSize=100"+(page==""?"":"&pageToken="+Uri.EscapeDataString(page)),"GET",null,r=>result=r);
            if(!result.ok){done(result);yield break;}
            var data=jsonObject(result.body);
            foreach(var doc in data["documents"] as JArray ?? new JArray()) {
                var s=decodeFields((JObject)doc["fields"]);total.sessionsPlayed++;if((bool)s["completed"])total.sessionsCompleted++;
                total.bestScore=Math.Max(total.bestScore,(long)s["score"]);total.highestLevel=Math.Max(total.highestLevel,(int)s["highestLevel"]);
            }
            page=(string)data["nextPageToken"]??"";
        }while(page!="");
        done(new response{status=200,body=JsonUtility.ToJson(total)});
    }
    private IEnumerator firebaseConsent(JObject value,Action<response> done)
    {
        response receipt=null,current=null;string id=(string)value["requestId"];
        yield return readDocument(playerPath,r=>current=r);if(!current.ok){done(current);yield break;}
        yield return readDocument(playerPath+"/consents/"+id,r=>receipt=r);
        if(receipt.ok) {
            if((string)documentFields(current)["consentReceiptId"]!=id || !sameFields(documentFields(receipt),value)){done(storeConflict());yield break;}
            yield return firebaseProfile(done);yield break;
        }
        if(receipt.status!=404){done(receipt);yield break;}
        value["schemaVersion"]=1;
        var writes=new JArray(documentWrite(playerPath+"/consents/"+id,value,true,null,false,"savedAt"),
            documentWrite(playerPath,new JObject{{"consentReceiptId",id}},false,(string)jsonObject(current.body)["updateTime"],true,"updatedAt"));
        yield return commitDocuments(writes,r=>current=r);
        if(!current.ok){done(current);yield break;}
        // Confirm the actual current receipt, not just a successful HTTP status.
        yield return firebaseProfile(done);
    }
    private IEnumerator firebaseTutorial(Action<response> done)
    {
        response result=null;yield return readDocument(playerPath,r=>result=r);if(!result.ok){done(result);yield break;}
        if((int)documentFields(result)["tutorialVersion"]<tutorialVersion) {
            yield return commitDocuments(new JArray(documentWrite(playerPath,new JObject{{"tutorialVersion",tutorialVersion}},false,(string)jsonObject(result.body)["updateTime"],true,"tutorialCompletedAt","updatedAt")),r=>result=r);
            if(!result.ok){done(result);yield break;}
        }
        yield return firebaseProfile(done);
    }
    private IEnumerator firebaseStart(JObject value,Action<response> done)
    {
        string path=playerPath+"/sessions/"+(string)value["sessionId"];
        response result=null;yield return readDocument(path,r=>result=r);
        if(result.ok){var s=documentFields(result);done((string)s["gameVersion"]==(string)value["gameVersion"] && (string)s["mode"]==(string)value["mode"] && (int)s["revision"]==0 && !(bool)s["completed"]?storeSuccess():storeConflict());yield break;}
        if(result.status!=404){done(result);yield break;}
        var session=new JObject{{"schemaVersion",2},{"gameVersion",value["gameVersion"]},{"mode",value["mode"]},{"consentReceiptId",profile.consentReceiptId},
            {"revision",0},{"score",0},{"highestLevel",0},{"elapsedSeconds",0},{"completed",false},
            {"startedAtUnixMs",null},{"endedAtUnixMs",null},{"endReason",null}};
        yield return commitDocuments(new JArray(documentWrite(path,session,true,null,false,"createdAt","updatedAt")),done);
    }
    private IEnumerator firebaseCheckpoint(JObject value,Action<response> done)
    {
        string path=playerPath+"/sessions/"+(string)value["sessionId"];value.Remove("sessionId");
        response result=null;yield return readDocument(path,r=>result=r);if(!result.ok){done(result);yield break;}
        var existing=documentFields(result);
        if((int)existing["schemaVersion"]==2) {
            foreach(string field in new[]{"startedAtUnixMs","endedAtUnixMs"})
                value[field]=(long?)value[field]>0?value[field]:JValue.CreateNull();
            value["endReason"]=string.IsNullOrEmpty((string)value["endReason"])?JValue.CreateNull():value["endReason"];
        } else {value.Remove("startedAtUnixMs");value.Remove("endedAtUnixMs");value.Remove("endReason");}
        if((int)existing["revision"]==(int)value["revision"]){done(sameFields(existing,value)?storeSuccess():storeConflict());yield break;}
        if((int)existing["revision"]+1!=(int)value["revision"]){done(storeConflict());yield break;}
        yield return commitDocuments(new JArray(documentWrite(path,value,false,(string)jsonObject(result.body)["updateTime"],true,"updatedAt")),done);
    }
    private IEnumerator firebaseTrial(JObject upload,Action<response> done)
    {
        string sessionPath=playerPath+"/sessions/"+(string)upload["sessionId"];
        var trial=(JObject)upload["trial"];var audio=trial["audioLoudness"] as JObject;
        int version=(int?)trial["schemaVersion"]??1;
        var notes=audio?["notes"] as JArray ?? new JArray();audio?.Remove("notes");
        trial.Remove("timestamp");trial["schemaVersion"]=version;trial["noteCount"]=notes.Count;
        for(int i=0;i<notes.Count;i++) {
            var note=(JObject)notes[i];note["index"]=i;note["schemaVersion"]=version;
            if(version==2)note.Remove("requestedTimeInSequenceSeconds");else note.Remove("startedAtMs");
        }
        string path=sessionPath+"/trials/"+(string)trial["roundIndex"];
        response result=null;yield return readDocument(path,r=>result=r);
        bool finalizing=result.ok && version==2 && (string)documentFields(result)["state"]=="started" && (string)trial["state"]=="resolved";
        if(result.ok && !finalizing) {
            if(!sameFields(documentFields(result),trial)){done(storeConflict());yield break;}
            for(int i=0;i<notes.Count;i++) {
                var note=(JObject)notes[i];
                yield return readDocument(path+"/notes/"+i,r=>result=r);
                if(!result.ok || !sameFields(documentFields(result),note)){done(result.ok?storeConflict():result);yield break;}
            }
            done(storeSuccess());yield break;
        }
        if(!finalizing && result.status!=404){done(result);yield break;}
        var writes=new JArray(documentWrite(path,trial,!finalizing,finalizing?(string)jsonObject(result.body)["updateTime"]:null,false,"savedAt"));
        for(int i=0;i<notes.Count;i++)writes.Add(documentWrite(path+"/notes/"+i,(JObject)notes[i],true));
        yield return commitDocuments(writes,r=>result=r);
        if(result.status==403) {
            response profileResult=null,session=null;
            yield return firebaseProfile(r=>profileResult=r);yield return readDocument(sessionPath,r=>session=r);
            if(profileResult.ok && session.ok) {
                var p=parse<profileRecord>(profileResult.body);var s=documentFields(session);
                bool expired=DateTimeOffset.TryParse((string)s["createdAt"],out var started) && started.AddHours(24)<DateTimeOffset.UtcNow;
                if(!p.researchCollectionEnabled || p.consentReceiptId!=(string)s["consentReceiptId"] || expired)
                    result=new response{status=403,body="{\"error\":\"research_not_allowed\"}"};
            }
        }
        done(result);
    }
}
