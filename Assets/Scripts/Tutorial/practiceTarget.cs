using UnityEngine;
// Tutorial staging only. Durability is exactly the normal one-projectile behavior.
public class practiceTarget : MonoBehaviour
{
    public bool moving;
    public float speed=2.4f;
    public bool holdAtReveal=true;
    public float revealY;
    public float minimumY=float.NegativeInfinity;
    public bool arrived => transform.position.y<=revealY+.001f;
    private void Update()
    {
        if(!moving)return;
        var position=transform.position;position.y-=speed*Time.deltaTime;
        if(holdAtReveal)position.y=Mathf.Max(position.y,revealY);
        position.y=Mathf.Max(position.y,minimumY);
        transform.position=position;transform.Rotate(0,0,35*Time.deltaTime);
    }
}

