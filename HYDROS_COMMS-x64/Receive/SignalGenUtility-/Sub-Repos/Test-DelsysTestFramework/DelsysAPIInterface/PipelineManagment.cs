using Aero.PipeLine;
using DelsysAPI.DelsysDevices;
using DelsysAPI.Events;
using DelsysAPI.Exceptions;
using DelsysAPI.Pipelines;
using DelsysTestLib.NIDAQ;
using System.Diagnostics;

namespace DelsysTestLib.DelsysAPIInterface
{
    public class PipelineManagement
    {
        public static PipelineManagement Instance { get; } = new PipelineManagement();

        // Delsys API / Pairing Sensors
        private System.Threading.CancellationTokenSource cancellationToken;
        // Pipeline fields
        private IDelsysDevice _deviceSource;
        private Pipeline _pipeline;
        // Holds collection data
        private List<List<double>> _data;
        // Metadata fields
        private int _totalLostPackets;
        private int _frameThroughput;
        private double _packetInterval;
        private double _streamTime = 0.0;
        private double _totalFrames = 0.0;

        public bool HasPipeline => _pipeline != null;

        public bool ScanCompleted { get; private set; }

        private PipelineManagement()
        {
            if (_pipeline == null)
            {
                InitPipeline();
            }
        }
        public void InitPipeline()
        {
            InitializeDataSource();
            LoadDataSource();
        }

        public async Task ShutdownAllSensors()
        {
            if (_pipeline != null)
            {
                await _pipeline.Stop();
                await _pipeline.DisarmPipeline();
                _pipeline.TrignoRfManager.TurnOffAllSensors();
            }
        }

        public void InitializeDataSource()
        {
            // Add your API key & license here 

            string key = "MIIBKjCB4wYHKoZIzj0CATCB1wIBATAsBgcqhkjOPQEBAiEA/////wAAAAEAAAAAAAAAAAAAAAD///////////////8wWwQg/////wAAAAEAAAAAAAAAAAAAAAD///////////////wEIFrGNdiqOpPns+u9VXaYhrxlHQawzFOw9jvOPD4n0mBLAxUAxJ02CIbnBJNqZnjhE50mt4GffpAEIQNrF9Hy4SxCR/i85uVjpEDydwN9gS3rM6D0oTlF2JjClgIhAP////8AAAAA//////////+85vqtpxeehPO5ysL8YyVRAgEBA0IABAPekLGWqUycYyJdJdlhipFnPft4/2bHwJike3/9xXq9nLyUpfOcAkm+vMp9RbrFdv/iqvX68VCJ/q0nGIUPz5I=";
            string license = "<License>  <Id>62028a34-1f14-4e36-9519-5ad94bf7a615</Id>  <Type>Trial</Type>  <Quantity>10</Quantity>  <LicenseAttributes>    <Attribute name=\"Software\">VS2012</Attribute>  </LicenseAttributes>  <ProductFeatures>    <Feature name=\"Sales\">True</Feature>    <Feature name=\"Billing\">False</Feature>  </ProductFeatures>  <Customer>    <Name>John Doe</Name>    <Email>johndoe@delsys.test.com</Email>  </Customer>  <Expiration>Tue, 13 Oct 2099 04:00:00 GMT</Expiration>  <Signature>MEUCIAsdXL4lG6yv/lj0XwV+ehWNqAkSt/+p113+pFgh3H7RAiEAuHjdN/t7x708kY/e/Skc+z20WRMvTch+wulK4NHYXT8=</Signature></License>";
            // The API uses a factory method to create the data source of your application.
            // This creates the factory method, which will then give the data source for your platform.
            // In this case, the platform is RF.
            var deviceSourceCreator = new DeviceSourcePortable(key, license);
            deviceSourceCreator.SetDebugOutputStream((str, args) => Trace.WriteLine(string.Format(str, args)));

            // Here is where we tell the factory method what type of data source we want to receive,
            // which we then set a reference to for future use.
            _deviceSource = deviceSourceCreator.GetDataSource(SourceType.TRIGNO_RF);

            // Here we use the key and license we previously loaded.
            _deviceSource.Key = key;
            _deviceSource.License = license;

            Trace.WriteLine("Data Source Init");
        }
        public void LoadDataSource()
        {
            if (_pipeline == null)
            {
                // Attempts to load device
                try
                {
                    // Create a Pipeline based on the datasource.
                    Debug.WriteLine("Creating pipeline...");
                    PipelineController.Instance.AddPipeline(_deviceSource);
                }
                // Catches exception if no base is detected
                catch (BaseDetectionFailedException e)
                {
                    Debug.WriteLine("Base detection failed: " + e.Message);
                    return;
                }

                // Create a reference to this Pipeline
                _pipeline = PipelineController.Instance.PipelineIds[0];

                // Define the time (in seconds) we want to spend scanning for paired sensors.
                _pipeline.TrignoRfManager.InformationScanTime = 5;

                // Register handlers for API component (sensor specific) events
                _pipeline.TrignoRfManager.ComponentAdded += ComponentAdded;
                _pipeline.TrignoRfManager.ComponentLost += ComponentLost;
                _pipeline.TrignoRfManager.ComponentRemoved += ComponentRemoved;
                _pipeline.TrignoRfManager.ComponentScanComplete += ComponentScanComplete;

                // Register handlers for API collection events
                _pipeline.CollectionStarted += CollectionStarted;
                _pipeline.CollectionDataReady += CollectionDataReady;
                _pipeline.CollectionComplete += CollectionComplete;
            }
        }
        public async Task ScanForSensors()
        {
            System.Diagnostics.Debug.WriteLine("# of components before scan: " + _pipeline.TrignoRfManager.Components.Count);

            // Remove any previously connected sensors from pipeline
            foreach (var comp in _pipeline.TrignoRfManager.Components)
            {
                await _pipeline.TrignoRfManager.DeselectComponentAsync(comp);
                _pipeline.TrignoRfManager.RemoveTrignoComponent(comp);
            }

            // Display scanned sensors user control.
            await _pipeline.Scan();

            System.Diagnostics.Debug.WriteLine("# of components after scan: " + _pipeline.TrignoRfManager.Components.Count);
            if (_pipeline.TrignoRfManager.Components.Count > 0)
            {
                _pipeline.TrignoRfManager.Components[0].SelectSampleMode(_pipeline.TrignoRfManager.Components[0].Configuration.SampleModes[7]);
                _pipeline.TrignoRfManager.SelectComponentAsync(_pipeline.TrignoRfManager.Components[0]).Wait();

                var dataLine = new DataLine(_pipeline);
                dataLine.ConfigurePipeline();
                _frameThroughput = PipelineController.Instance.GetFrameThroughput();
            }
        }

        public async Task<bool> ScanForPairRequestAsync(int sensorNumber = 1, int NumberOfSensors = 1)
        {
            InitPipeline();

            System.Diagnostics.Debug.WriteLine("# of components before scan: " + _pipeline.TrignoRfManager.Components.Count);
            cancellationToken = new System.Threading.CancellationTokenSource();
            _pipeline.TrignoRfManager.InformationScanTime = 1;
            _pipeline.TrignoRfManager.PostPairScanTime = 1;

            if (_pipeline.TrignoRfManager.Components.Count < NumberOfSensors)
            {
                return await _pipeline.TrignoRfManager.AddTrignoComponent(cancellationToken.Token, sensorNumber, false);
            }

            System.Diagnostics.Debug.WriteLine("# of components after pair: " + _pipeline?.TrignoRfManager.Components.Count);

            return false;
        }

        public void ArmAndConfigPipeline()
        {
            foreach (var comp in _pipeline.TrignoRfManager.Components)
            {
                comp.SelectSampleMode(comp.Configuration.SampleModes[7]);
                _pipeline.TrignoRfManager.SelectComponentAsync(comp).Wait(); // Allocates the component to the pipeline
            }
            if (_pipeline?.TrignoRfManager.Components.Count > 0)
            {
                var dataLine = new DataLine(_pipeline);
                dataLine.ConfigurePipeline();

                // Get the frame throughput (the number of Trigno frames passed from the API at a time)
                _frameThroughput = PipelineController.Instance.GetFrameThroughput();
            }
        }

        public async Task ResetPipeline()
        {
            if (_pipeline != null)
            {
                await _pipeline.Stop();
                await _pipeline.DisarmPipeline();
                _totalFrames = 0;
                _totalLostPackets = 0;
                _streamTime = 0.0;

                _pipeline.TrignoRfManager.TurnOffAllSensors();
                _pipeline.TrignoRfManager.Components.Clear();
                PipelineController.Instance.RemovePipeline(0);
                _pipeline = null;
            }
        }

        public string GetPipelineState()
        {
            if (_pipeline != null)
            {
                return _pipeline.CurrentState.ToString();
            }
            else
            {
                return "Pipeline not loaded";
            }
        }

        public async Task ManualPairAndConnect()
        {
            InitPipeline();
            cancellationToken = new System.Threading.CancellationTokenSource();

            ScanCompleted = false;

            _pipeline.TrignoRfManager.InformationScanTime = 1;
            _pipeline.TrignoRfManager.PostPairScanTime = 1;

            if (_pipeline.TrignoRfManager.Components.Count < 1) // pair sensors
            {
                await _pipeline.TrignoRfManager.AddTrignoComponent(cancellationToken.Token, 1, false);
            }

            await _pipeline.Scan(); // scan / connect to sensors

            while (!ScanCompleted) // wait for scan complete
            {
                Task.Delay(1).Wait();
            }
            ArmAndConfigPipeline(); // configure pipeline and arms sensors
            await _pipeline.Start(); // starts the pipeline
        }

        public int GetNumberSensorsConnected()
        {
            if (_pipeline == null)
                return 0;
            return _pipeline.TrignoRfManager.Components.Count;
        }

        public void CancelPair()
        {
            cancellationToken.Cancel();

            if (_pipeline != null)
            {
                if (_pipeline.CurrentState == Pipeline.ProcessState.Running || _pipeline.CurrentState == Pipeline.ProcessState.Connected)
                {
                    _pipeline.Stop().Wait();
                    _pipeline.DisarmPipeline().Wait();
                }
                ResetPipeline().Wait();
            }
        }

        public async Task PairAndStreamHallAsync(string charge, string induct)
        {
            FixtureFramework _fixture = FixtureFramework.Instance;
            InitPipeline();
            Trace.WriteLine("Pipeline init - Pair and Stream Hall async");

            _fixture.SetDO(true, new string[] { charge }); // make it think that it was charging
            await Task.Delay(3000);
            _fixture.SetDO(false, new string[] { charge }); // Not charging anymore
            await Task.Delay(1000);
            Trace.WriteLine("Not charging");

            _fixture.SetDO(true, new string[] { induct }); // wake up the sensor
            await Task.Delay(1000);
            _fixture.SetDO(false, new string[] { induct });
            await Task.Delay(1000);
            Trace.WriteLine("Sensor Awake");

            _fixture.SetDO(true, new string[] { induct }); // pairing mode
            await Task.Delay(1000);
            _fixture.SetDO(false, new string[] { induct });
            Trace.WriteLine("Pairing Mode");

            cancellationToken = new System.Threading.CancellationTokenSource();
            ScanCompleted = false;

            _pipeline.TrignoRfManager.InformationScanTime = 1;
            _pipeline.TrignoRfManager.PostPairScanTime = 1;

            _ = DelayHall(induct, _fixture);
            cancellationToken.CancelAfter(5000);
            await _pipeline.TrignoRfManager.AddTrignoComponent(cancellationToken.Token, 1, false);
            if (!(_pipeline.TrignoRfManager.Components.Count > 0)) // did we get canceled?
            {
                Trace.WriteLine("Pairing canceled");
                return;
            }
            Trace.WriteLine("Paired device");

            await _pipeline.Scan(); // scan / connect to sensors

            while (!ScanCompleted) // wait for scan complete
            {
                Trace.WriteLine("Waiting for scan to complete");
                await Task.Delay(100); // Add a small delay or use await Task.Yield()
            }

            ArmAndConfigPipeline(); // configure pipeline and arms sensors
            await _pipeline.Start(); // starts the pipeline
            Trace.WriteLine("Pipeline started");
        }

        public async Task PairAndConnect(string charge, string induct)
        {
            FixtureFramework _fixture = FixtureFramework.Instance;
            InitPipeline();

            _fixture.SetDO(true, new string[] { charge }); // make it think that it was charging
            Task.Delay(3000).Wait();
            _fixture.SetDO(false, new string[] { charge }); // Not charging anymore
            Task.Delay(1000).Wait();

            _fixture.SetDO(true, new string[] { induct }); // wake up the sensor
            Task.Delay(1000).Wait();
            _fixture.SetDO(false, new string[] { induct });
            Task.Delay(1000).Wait();

            _fixture.SetDO(true, new string[] { induct }); // pairing mode
            Task.Delay(1000).Wait();
            _fixture.SetDO(false, new string[] { induct });

            cancellationToken = new System.Threading.CancellationTokenSource();

            ScanCompleted = false;

            _pipeline.TrignoRfManager.InformationScanTime = 1;
            _pipeline.TrignoRfManager.PostPairScanTime = 1;

            _ = DelayHall(induct, _fixture);
            cancellationToken.CancelAfter(5000);
            await _pipeline.TrignoRfManager.AddTrignoComponent(cancellationToken.Token, 1, false);

            if (!(_pipeline.TrignoRfManager.Components.Count > 0)) // did we get canceled?
                return;

            await _pipeline.Scan(); // scan / connect to sensors


            while (!ScanCompleted) // wait for scan complete
            {

            }
        }
        public async Task ConfigureAndStartStream()
        {
            ArmAndConfigPipeline(); // configure pipeline and arms sensors
            await _pipeline.Start(); // starts the pipeline
        }
        public async Task DelayHall(string induct, FixtureFramework _ff)
        {
            Trace.WriteLine("Delaying Hall");
            await Task.Delay(2000);
            _ff.SetDO(true, new string[] { induct }); // time to pair
            await Task.Delay(1000);
            _ff.SetDO(false, new string[] { induct });
            Trace.WriteLine("Hall Delayed");
        }

        public int GetSensorID() => (int)(_pipeline.TrignoRfManager.Components[0].Properties.Sid);

        public string GetBaseStationID()
        {
            Dictionary<string, string> systemInfo = _pipeline.DataSourceInfo[SourceType.TRIGNO_RF][0];
            if (systemInfo.TryGetValue("Base ID", out string baseId))
            {
                Trace.WriteLine($"Base ID: {baseId}");
                return baseId;
            }
            else if (systemInfo.TryGetValue("Dongle ID", out string dongleId))
            {
                Trace.WriteLine($"Dongle ID: {dongleId}");
                return dongleId;
            }
            else
            {
                return "";
            }
        }

        #region API Data Collection Event Handlers
        public void CollectionStarted(object sender, CollectionStartedEvent e)
        {
            _data = new List<List<double>>();
            _totalFrames = 0;
            _totalLostPackets = 0;

            int totalChannels = 0;

            // Recreate the list of data channels for recording.
            // First, iterate across all components
            for (int i = 0; i < _pipeline.TrignoRfManager.Components.Count; i++)
            {
                // then across all channels within each component.
                for (int j = 0; j < _pipeline.TrignoRfManager.Components[i].TrignoChannels.Count; j++)
                {
                    if (_data.Count <= totalChannels)
                    {
                        _data.Add(new List<double>());
                    }
                    else
                    {
                        _data[totalChannels] = new List<double>();
                    }
                    if (_packetInterval == 0)
                    {
                        _packetInterval = _pipeline.TrignoRfManager.Components[i].TrignoChannels[j].FrameInterval * _frameThroughput;
                    }
                    totalChannels++;
                }
            }

        }

        public void CollectionDataReady(object sender, ComponentDataReadyEventArgs e)
        {
            // Increment timer
            _streamTime += _packetInterval;

            int lostPackets = 0;

            // Checks to see if any of the packets were lost during the stream.
            // Loops through each frame data, since the API passes the data as n packets/frames, where n is the value of frameThroughput.
            // We need to check all frames to see if any data is lost from any of them.
            for (int k = 0; k < e.Data.Length; k++)
            {
                // Loops through each sensor.
                for (int i = 0; i < e.Data[k].SensorData.Length; i++)
                {
                    // Checks to see if any of the data in a sensor is lost.
                    if (e.Data[k].SensorData[i].IsDroppedPacket)
                    {
                        lostPackets++;
                    }
                }
            }
            _totalLostPackets += lostPackets;
            _totalFrames += _frameThroughput * e.Data[0].SensorData.Length;

            // Determines the column that the data will be inserted into.
            int columnIndex = 0;
            for (int k = 0; k < e.Data.Length; k++)
            {
                // Loops through each connected sensors.
                for (int i = 0; i < e.Data[k].SensorData.Length; i++)
                {
                    // Loops through each channel for each sensor.
                    for (int j = 0; j < e.Data[k].SensorData[i].ChannelData.Length; j++)
                    {
                        // Loops through the data of each channel.
                        foreach (var val in e.Data[k].SensorData[i].ChannelData[j].Data)
                        {
                            // Adds the data at the current column index.
                            _data[columnIndex].Add(val);
                        }
                        columnIndex++;
                    }
                }
                // Resets column index so the next data in the second Trigno Frame gets added to the right channels.
                columnIndex = 0;
            }
        }
        public void CollectionComplete(object sender, CollectionCompleteEvent e)
        {

        }
        #endregion
        #region API Component Event Handlers
        public void ComponentAdded(object sender, ComponentAddedEventArgs e)
        {
            Debug.WriteLine("ComponentAdded");
        }

        public void ComponentLost(object sender, ComponentLostEventArgs e)
        {
            Debug.WriteLine("ComponentLost");
        }

        public void ComponentRemoved(object sender, ComponentRemovedEventArgs e)
        {
            Debug.WriteLine("ComponentRemoved");
        }

        public void ComponentScanComplete(object sender, ComponentScanCompletedEventArgs e)
        {
            // Check if no sensors were detected in scan
            if (e.ComponentDictionary.IsEmpty)
            {
                // Propmt user to try again if none found
                return;
            }
            else
            {
                ScanCompleted = true;
            }

            int sensorIndex = 0;
            foreach (var comp in _pipeline.TrignoRfManager.Components)
            {
                comp.SelectSampleMode(comp.Configuration.SampleModes[7]);
                sensorIndex++;
            }
        }

        public async Task ConnectSensor()
        {
            await _pipeline.Scan(); // scan / connect to sensors
        }

        public void TurnOnSensor(string charge, string induct)
        {
            FixtureFramework _fixture = FixtureFramework.Instance;

            _fixture.SetDO(true, new string[] { charge }); // make it think that it was charging
            Task.Delay(3000).Wait();
            _fixture.SetDO(false, new string[] { charge }); // Not charging anymore
            Task.Delay(1000).Wait();

            _fixture.SetDO(true, new string[] { induct }); // wake up the sensor
            Task.Delay(1000).Wait();
            _fixture.SetDO(false, new string[] { induct });
            Task.Delay(1000).Wait();

        }
        #endregion
    }
}
