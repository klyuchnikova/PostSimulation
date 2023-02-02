using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SkladModel;
using System.Linq;
using ExtendedXmlSerializer.Configuration;
using ExtendedXmlSerializer;
using System.IO;
using System.Xml;
using System;
using Object = UnityEngine.Object;
using Unity.VisualScripting;
using ControlModel;

public class main : MonoBehaviour
{

    public static Vector2 getPosition(double x, double y)
    {
        return new Vector2((float)x - 10, -(float)y+8);
    }

    public Transform[] brick;
    public Transform AntBotTransform;
    Dictionary<string, AntBotUnity> antsBot = new Dictionary<string, AntBotUnity>();
    private SkladWrapper skladWrapper;
    Sklad sklad;
    DateTime startTime;
    AntStateChange asc;
    public Transform panel;
    AntStateChange[] logs;

    // Start is called before the first frame update
    void Start()
    {
        System.Random rnd = new System.Random(DateTime.Now.Millisecond);
        SkladWrapper skladWrapper = new SkladWrapper(@"wms-config.xml", false);
        skladWrapper.AddLogger();
        skladWrapper.AddSklad();
        skladWrapper.AddAnts();
        new MoveSort(skladWrapper).Run(TimeSpan.FromSeconds(0));
        //SkladLogger logger = (SkladLogger)skladWrapper.objects.First(x => x is SkladLogger);
        //logs = logger.logs.ToArray();
        logs = SkladWrapper.DeserializeXML<AntStateChange[]>(File.ReadAllBytes("log_unity.xml"));


        sklad = (Sklad) skladWrapper.objects.First(x=>x is Sklad);

        foreach (var yl in sklad.skladLayout)
        {
            int y = yl.Key;
            foreach (var xl in yl.Value) {
                Transform br = Object.Instantiate(brick[xl.Value]);
                br.SetParent(panel);
                int x = xl.Key;
                br.SetPositionAndRotation(getPosition(x, y), Quaternion.identity);
            }
        }
        startTime = DateTime.Now;
        asc = logs[0];
    }

    int count = 0;
    void Update()
    {
        TimeSpan current = DateTime.Now - startTime;
        if (current.TotalSeconds > asc.lastUpdated) 
        {
            count++;
            //Debug.Log(logs[count].id + " " + logs[count].lastUpdated);
            if (asc.command == "Create AntBot")
            {
                Transform ab = Instantiate(AntBotTransform);
                ab.SetParent(panel);
                antsBot.Add(asc.uid, ab.GetComponent<AntBotUnity>());
                antsBot[asc.uid].antStateChange = asc;
                antsBot[asc.uid].SetPosition();
                antsBot[asc.uid].startTime = startTime;
            }
            else
            {
                antsBot[asc.uid].antStateChange = asc;
            }

            if (asc.command == "Rotate")
            {
                StartCoroutine(antsBot[asc.uid].RotateMe(new Vector3(0, 0, 90), 4));
            }




                asc = logs[count];
        }
    }
}
