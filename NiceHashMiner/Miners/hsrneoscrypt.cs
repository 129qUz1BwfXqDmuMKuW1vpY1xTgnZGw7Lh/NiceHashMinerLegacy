﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using NiceHashMiner.Configs;
using NiceHashMiner.Enums;
using NiceHashMiner.Devices;
using NiceHashMiner.Miners.Grouping;
using NiceHashMiner.Miners.Parsing;
using System.Threading.Tasks;
using System.Threading;
using NiceHashMiner.Algorithms;

namespace NiceHashMiner.Miners
{
    public class hsrneoscrypt : Miner
    {
        private int benchmarkTimeWait = 11 * 60;
        public hsrneoscrypt() : base("hsrneoscrypt_NVIDIA") { }

        bool benchmarkException {
            get {
                return MiningSetup.MinerPath == MinerPaths.Data.hsrneoscrypt;
            }
        }

        protected override int GetMaxCooldownTimeInMilliseconds() {
            if (this.MiningSetup.MinerPath == MinerPaths.Data.hsrneoscrypt) {
                return 60 * 1000 * 12; // wait for hashrate string
            }
            return 60 * 1000 * 12; // 11 minute max
        }

        public override void Start(string url, string btcAdress, string worker)
        {
            if (!IsInit) {
                Helpers.ConsolePrint(MinerTag(), "MiningSetup is not initialized exiting Start()");
                return;
            }
            string username = GetUsername(btcAdress, worker);

            //IsAPIReadException = MiningSetup.MinerPath == MinerPaths.Data.hsrneoscrypt;
            IsApiReadException = false; //** in miner 

            /*
            string algo = "";
            string apiBind = "";
            if (!IsAPIReadException) {
                algo = "--algo=" + MiningSetup.MinerName;
                apiBind = " --api-bind=" + APIPort.ToString();
            }
            */
            /*
            LastCommandLine = algo +
                                  " --url=" + url +
                                  " --userpass=" + username + ":x " +
                                  apiBind + " " +
                                  ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.NVIDIA) +
                                  " --devices ";
*/
            //add failover
            string alg = url.Substring(url.IndexOf("://") + 3, url.IndexOf(".") - url.IndexOf("://") - 3);
            string port = url.Substring(url.IndexOf(".com:") + 5, url.Length - url.IndexOf(".com:") - 5);
            /*
            LastCommandLine = algo +
                              " --url=" + url +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".hk.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".in.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".jp.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".usa.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".br.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              " --url stratum+tcp://" + alg + ".eu.nicehash.com:" + port +
                              " --userpass=" + username + ":x " +
                              apiBind + " " +
                                  ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.NVIDIA) +
                                  " --devices ";

            LastCommandLine += GetDevicesCommandString();
*/

            LastCommandLine = " --url=" + url +
                                  " --user=" + username +
                          " -p x " +
                                  ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.NVIDIA) +
                                  " --devices ";
            LastCommandLine += GetDevicesCommandString();
            ProcessHandle = _Start();
        }

        protected override void _Stop(MinerStopType willswitch) {
            Stop_cpu_ccminer_sgminer_nheqminer(willswitch);
        }

        // new decoupled benchmarking routines
        #region Decoupled benchmarking routines

        protected override string BenchmarkCreateCommandLine(Algorithm algorithm, int time) {

            string url = Globals.GetLocationUrl(algorithm.NiceHashID, Globals.MiningLocation[ConfigManager.GeneralConfig.ServiceLocation], this.ConectionType);

            string username = Globals.DemoUser;

            if (ConfigManager.GeneralConfig.WorkerName.Length > 0)
                username += "." + ConfigManager.GeneralConfig.WorkerName.Trim();

            string CommandLine =  " --url=" + url +
                                  " --user=" + Globals.DemoUser +
                          " -p x " +
                                  ExtraLaunchParametersParser.ParseForMiningSetup(
                                                                MiningSetup,
                                                                DeviceType.NVIDIA) +
                                  " --devices ";
            CommandLine += GetDevicesCommandString();

            Helpers.ConsolePrint(MinerTag(), CommandLine);

            return CommandLine;
        }

        protected override bool BenchmarkParseLine(string outdata) {

            Helpers.ConsolePrint(MinerTag(), outdata);
            if (benchmarkException)
            {

                if (outdata.Contains("speed is "))
                {
                    int st = outdata.IndexOf("speed is ");
                    int end = outdata.IndexOf("kH/s");
                    //      int len = outdata.Length - speedLength - st;

                    //          string parse = outdata.Substring(st, len-1).Trim();
                    //          double tmp = 0;
                    //          Double.TryParse(parse, NumberStyles.Any, CultureInfo.InvariantCulture, out tmp);

                    // save speed
                    //       int i = outdata.IndexOf("Benchmark:");
                    //       int k = outdata.IndexOf("/s");
                    string hashspeed = outdata.Substring(st + 9, end - st - 9);
                    /*
                    int b = hashspeed.IndexOf(" ");
                       if (hashspeed.Contains("k"))
                           tmp *= 1000;
                       else if (hashspeed.Contains("m"))
                           tmp *= 1000000;
                       else if (hashspeed.Contains("g"))
                           tmp *= 1000000000;

                   }
                   */

                    double speed = Double.Parse(hashspeed, CultureInfo.InvariantCulture);
                    BenchmarkAlgorithm.BenchmarkSpeed = speed * 1000;
                    BenchmarkSignalFinnished = true;
                }
            }
            return false;
        }

        protected override void BenchmarkOutputErrorDataReceivedImpl(string outdata) {
            CheckOutdata(outdata);
        }

        #endregion // Decoupled benchmarking routines

        public override async Task<ApiData> GetSummaryAsync() {
            // CryptoNight does not have api bind port
            ApiData hsrData = new ApiData(MiningSetup.CurrentAlgorithmType);
            hsrData.Speed = 0;
            if (IsApiReadException) {
                // check if running
                if (ProcessHandle == null) {
                    CurrentMinerReadStatus = MinerApiReadStatus.RESTART;
                    Helpers.ConsolePrint(MinerTag(), ProcessTag() + " Could not read data from hsrminer Proccess is null");
                    return null;
                }
                try {
                    var runningProcess = Process.GetProcessById(ProcessHandle.Id);
                } catch (ArgumentException ex) {
                    CurrentMinerReadStatus = MinerApiReadStatus.RESTART;
                    Helpers.ConsolePrint(MinerTag(), ProcessTag() + " Could not read data from hsrminer reason: " + ex.Message);
                    return null; // will restart outside
                } catch (InvalidOperationException ex) {
                    CurrentMinerReadStatus = MinerApiReadStatus.RESTART;
                    Helpers.ConsolePrint(MinerTag(), ProcessTag() + " Could not read data from hsrminer reason: " + ex.Message);
                    return null; // will restart outside
                }

                var totalSpeed = 0.0d;
                foreach (var miningPair in MiningSetup.MiningPairs) {
                    var algo = miningPair.Device.GetAlgorithm(MinerBaseType.hsrneoscrypt, AlgorithmType.NeoScrypt, AlgorithmType.NONE);
                    if (algo != null) {
                        totalSpeed += algo.BenchmarkSpeed;
                    }
                }

               // hsrData.Speed = totalSpeed;
               // return hsrData;
            }

              return await GetSummaryAsync();
            //return hsrData;
        }
    }
}
