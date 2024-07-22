using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace hankun.udp
{
    public enum Command
    {
        SetState = 1000,
        StartSendFile = 1004,
        StopSendFile = 1005,
        FileData = 1006
    }

    public class UDPCommand : MonoBehaviour
    {
        public static void SendCommand(Command command, string msg = "", bool isByte = false, byte[] bytes = null)
        {
            if (UDPManager.Instance != null)
            {
                if (isByte)
                {
                    var cmd = System.Text.Encoding.UTF8.GetBytes(((int) command).ToString());
                    var data = new byte[cmd.Length + bytes.Length];
                    Array.Copy(cmd, data, cmd.Length);
                    Array.Copy(bytes, 0, data, cmd.Length, bytes.Length);
                    UDPManager.Instance.Send(data);
                    Debug.Log("Send Command: " + command + " Bytes:" + bytes.Length);
                }
                else
                {
                    var str = (int) command + msg;
                    UDPManager.Instance.Send(System.Text.Encoding.UTF8.GetBytes(str));
                    Debug.Log("Send Command: " + command + " " + msg);
                }
            }
        }

        public static byte[] GetHash(string path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            return md5.ComputeHash(stream);
        }
    }
}