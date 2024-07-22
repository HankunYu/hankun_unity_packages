using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace hankun.udp
{
    public class DataDecoder : MonoBehaviour
    {
        public static T Decode<T>(string data)
        {
            if (typeof(T) == typeof(bool))
            {
                var result = bool.TryParse(data, out var value) && value;
                return (T) Convert.ChangeType(result, typeof(bool));
            }

            return default;
        }
    }
}
