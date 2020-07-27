using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Varjo;

namespace VarjoExample
{
    public class VarjoMultiClient : MonoBehaviour
    {
        [SerializeField] int defaultPriority = 0;
        [SerializeField] TextMesh text;
        [SerializeField] Transform cube;

        void Start()
        {
            // Set priority from command line arguments
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--priority" || args[i] == "-prio")
                {
                    SetPriority(int.Parse(args[i + 1]));
                    return;
                }
            }

            // If there was no priority set in command line,
            // use default priority set in editor
            SetPriority(defaultPriority);
        }

        void SetPriority(int priority)
        {
            transform.position += Vector3.up * priority * 0.1f;
            text.text += priority;
            VarjoPlugin.SetSessionPriority(priority);
        }

        void Update()
        {
            cube.Rotate(Vector3.up * 50.0f * Time.deltaTime, Space.World);
        }
    }
}
