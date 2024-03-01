using UnityEngine;

public class Paddle : MonoBehaviour
{
    public float speed = 10f;

    public Transform anchorTop;
    public Transform anchorMiddle;
    public Transform anchorBottom;

    public bool discreteMovements = false;
    public float discreteSpeed = 200f;

    public float topBound = 3.8f;
    public float bottomBound = -3.8f;

    private int currentAnchor = 2;

    // Update is called once per frame
    void Update()
    {
        Vector2 pos = transform.position;

        if (pos.y > topBound)
            pos.y = topBound;
        if (pos.y < bottomBound)
            pos.y = bottomBound;

        transform.position = pos;
    }

    

    public void MoveToAnchor(int anchor)
    {
        anchor = anchor == 0? 1 : anchor;
        anchor = anchor == 4? 3 : anchor;

        if(anchor == 1)
        {
            currentAnchor = 1;
            transform.position = anchorTop.position;
        } 
        else if(anchor == 2)
        {
            currentAnchor = 2;
            transform.position = anchorMiddle.position;
            //Vector3 direction = (anchorMiddle.position - transform.position).normalized;
            //transform.Translate(direction * discreteSpeed * Time.deltaTime);
        } 
        else if(anchor == 3)
        {
            currentAnchor = 3;
            transform.position = anchorBottom.position;
            //Vector3 direction = (anchorBottom.position - transform.position).normalized;
            //transform.Translate(direction * discreteSpeed * Time.deltaTime);
        }
    }

    public void MoveUp(float paddleSpeed)
    {
        if(discreteMovements)
        {
            MoveToAnchor(currentAnchor - 1);
            
        } else 
        {
            transform.Translate(Vector3.up * paddleSpeed * Time.deltaTime);
        }
    }

    public void MoveDown(float paddleSpeed)
    {
        if(discreteMovements)
        {
            MoveToAnchor(currentAnchor + 1);
        } else 
        {
            transform.Translate(Vector3.down * paddleSpeed * Time.deltaTime);
        }
    }

}
