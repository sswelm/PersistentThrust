using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class test : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var list = GetComponents<Component>();
        for (int i = 0; i < list.Length; i++)
        {
            Debug.Log(list[i].name);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
