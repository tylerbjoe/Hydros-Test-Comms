// See https://aka.ms/new-console-template for more information

//using DelsysTestLib.NIDAQ;
//using DelsysTestLib.Util;
using DelsysNICommon;
using System;
using System.Diagnostics;
using System.Windows;


Console.WriteLine("got instance");
//FixtureFramework ff = FixtureFramework.Instance;
Console.WriteLine("connect NI");
//ff.GetNIConnected();

NIDAQController ctrl = new NIDAQController();

ctrl.GetConnectedNIDevices();

Console.WriteLine("NI connected");


