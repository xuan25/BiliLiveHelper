﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BiliLiveHelper
{
    class ProformanceMonitor
    {
        public delegate void ProformanceDelegate(uint percentage);
        public event ProformanceDelegate CpuProformanceRecieved;
        public event ProformanceDelegate GpuProformanceRecieved;

        public ProformanceMonitor()
        {

        }

        public bool[] StartMonitoring()
        {
            bool cpu = StartCPU();
            bool gpu = StartGPU();
            return new bool[] { cpu, gpu };
        }

        public void StopMonitoring()
        {
            cpuMonitoringThread.Abort();
            cpuMonitoringThread.Join();
            gpuMonitoringProcess.Kill();
        }

        Thread cpuMonitoringThread;
        private bool StartCPU()
        {
            PerformanceCounter cpuPerformanceCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            cpuMonitoringThread = new Thread(delegate ()
            {
                while (true)
                {
                    CpuProformanceRecieved?.Invoke((uint)Math.Round(cpuPerformanceCounter.NextValue()));
                    Thread.Sleep(1000);
                }
            });
            cpuMonitoringThread.Start();
            return true;
        }

        Process gpuMonitoringProcess;
        private bool StartGPU()
        {
            if (!File.Exists(@"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe"))
                return false;
            gpuMonitoringProcess = new Process();
            gpuMonitoringProcess.StartInfo = new ProcessStartInfo
            {
                FileName = @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
                Arguments = "--query-gpu=utilization.gpu --format=csv,noheader,nounits -l 1",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            gpuMonitoringProcess.OutputDataReceived += GpuMonitoringProcess_OutputDataReceived;
            gpuMonitoringProcess.Start();
            gpuMonitoringProcess.BeginOutputReadLine();
            return true;
        }

        private void GpuMonitoringProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            uint.TryParse(e.Data, out uint p);
            GpuProformanceRecieved?.Invoke(p);
        }
    }
}
