using SkladModel;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AntBotUnity : MonoBehaviour
{

    public AntStateChange antStateChange = new AntStateChange();
    internal DateTime startTime;

    public void SetPosition()
    {
        SetPosition(main.getPosition(antStateChange.xCoordinate, antStateChange.yCoordinate));
        
    }

    public void SetPosition(Vector2 vector2)
    {
        transform.SetPositionAndRotation(vector2, Quaternion.identity);
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        double shift = (DateTime.Now - startTime - antStateChange.lastUpdated).TotalSeconds;
        if (antStateChange.xSpeed != 0 || antStateChange.ySpeed != 0)
        {
            SetPosition(main.getPosition((antStateChange.xCoordinate + antStateChange.xSpeed * shift),
                                         (antStateChange.yCoordinate + antStateChange.ySpeed * shift)));
        }      
    }
}
