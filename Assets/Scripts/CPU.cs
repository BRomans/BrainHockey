using UnityEngine;

public class CPU : Paddle
{
    public Transform ball;
  
    public Transform aiTrigger;
    // Update is called once per frame
    void Update()
    {
        if (ball.position.y > aiTrigger.position.y && ball.position.x > aiTrigger.position.x)
        {
            MoveUp(speed);
        }
        else if (ball.position.y < aiTrigger.position.y && ball.position.x > aiTrigger.position.x)
        {
            MoveDown(speed);
        }

        Vector2 pos = transform.position;

        if (pos.y > topBound)
            pos.y = topBound;
        if (pos.y < bottomBound)
            pos.y = bottomBound;

        transform.position = pos;
    }
}
