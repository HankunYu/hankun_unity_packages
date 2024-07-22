using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
// using Sirenix.OdinInspector;
using UnityEngine;

namespace hankun.udp
{
    public class FileTransfer : MonoBehaviour
    {
        public static FileTransfer Instance;
        public bool customReceivePath = false;

        // [InfoBox("Custom path is relative to StreamingAssets folder. Start without '/' or '\\'.")]
        // [ShowIf("customReceivePath")]
        public string customPath;

        private string _receivePath;
        private List<byte[]> _fileData = new List<byte[]>();
        private string _fileName;
        private int _fileLength;

        // [ReadOnly] 
        [Range(0, 1)] public float progress = 0;

        [Range(1024, 65500)] public int packageSize = 1024;

        // [InfoBox(
        //     "A higher value will increase the speed of file transfer, but will also increase the chance of data loss.")]
        // [ReadOnly]
        public bool isSending = false;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
            _receivePath = Application.streamingAssetsPath + "/" + customPath;
        }

        private void Start()
        {
            UDPManager.Instance.OnReceiveData += FileTransferListener;
        }

        private void FileTransferListener(string data, Command command, byte[] bytes)
        {
            if (command != Command.StartSendFile) return;
            _fileData.Clear();
            _fileName = data.Split('|')[0];
            _fileLength = int.Parse(data.Split('|')[1]);
            UDPManager.Instance.OnReceiveData += ReceiveData;
        }

        private void ReceiveData(string data, Command command, byte[] bytes)
        {
            switch (command)
            {
                case Command.FileData:
                    _fileData.Add(bytes);
                    break;
                case Command.StopSendFile:
                {
                    UDPManager.Instance.OnReceiveData -= ReceiveData;
                    var fileBytes = new byte[_fileLength];
                    var index = 0;
                    foreach (var fileData in _fileData)
                    {
                        Array.Copy(fileData, 0, fileBytes, index, fileData.Length);
                        index += fileData.Length;
                    }

                    File.WriteAllBytes(_receivePath + "/" + _fileName, fileBytes);
                    var hash = UDPCommand.GetHash(_receivePath + "/" + _fileName);
                    if (hash == bytes)
                        Debug.Log("File Transfer Success");
                    else
                        Debug.Log("File Transfer Failed");
                    break;
                }
            }
        }

        // [Button]
        public void SendFile(string path)
        {
            if (isSending) return;
            isSending = true;
            var bytes = File.ReadAllBytes(path);
            var fileName = Path.GetFileName(path);
            UDPCommand.SendCommand(Command.StartSendFile, fileName + "|" + bytes.Length);
            var packages = new List<byte[]>();
            var packageCount = bytes.Length / packageSize + 1;
            for (int i = 0; i < packageCount; i++)
            {
                var package = new byte[i == packageCount - 1 ? bytes.Length - i * packageSize : packageSize];
                Array.Copy(bytes, i * packageSize, package, 0,
                    i == packageCount - 1 ? bytes.Length - i * packageSize : packageSize);
                packages.Add(package);
            }

            var hash = UDPCommand.GetHash(path);
            StartCoroutine(SendPackages(packages, hash));
        }

        private IEnumerator SendPackages(List<byte[]> packages, byte[] hash)
        {

            yield return new WaitForSeconds(0.01f);
            foreach (var package in packages)
            {
                UDPCommand.SendCommand(Command.FileData, "", true, package);
                yield return new WaitForSeconds(0.01f);
                progress = (float) packages.IndexOf(package) / packages.Count;
            }

            //compare hash
            UDPCommand.SendCommand(Command.StopSendFile, "", true, hash);
            isSending = false;
        }
    }
}
