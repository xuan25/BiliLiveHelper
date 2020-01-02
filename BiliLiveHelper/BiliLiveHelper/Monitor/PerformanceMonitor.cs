using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace BiliLiveHelper.Monitor
{
    class PerformanceMonitor
    {
        public delegate void ProformanceDelegate(uint percentage);
        public event ProformanceDelegate CpuProformanceRecieved;
        public event ProformanceDelegate GpuProformanceRecieved;

        public PerformanceMonitor()
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
            try
            {
                if (cpuMonitoringThread != null)
                    cpuMonitoringThread.Abort();
                if (gpuMonitoringProcess != null)
                    gpuMonitoringProcess.Kill();
            }
            catch (Exception)
            {
                
            }
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
