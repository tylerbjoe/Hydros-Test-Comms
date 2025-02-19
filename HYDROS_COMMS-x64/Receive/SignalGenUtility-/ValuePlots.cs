using System;
using System.Diagnostics;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.RenderableSeries;

namespace DelsysSigNIalGen
{
    public class ValuePlot
    {
        private static XyDataSeries<int, int> dataSeries1;
        private static XyDataSeries<int, int> dataSeries2;

        // Event to notify UI when new SPO2 value is available
        public static event Action<int> OnSpo2Updated;
        public static event Action<int> OnHRUpdated;

        public static void InitializeValuePlot(FastLineRenderableSeries lineRenderableSeries1, FastLineRenderableSeries lineRenderableSeries2)
        {
            dataSeries1 = new XyDataSeries<int, int> { SeriesName = "Hr" };
            dataSeries2 = new XyDataSeries<int, int> { SeriesName = "Spo2" };

            lineRenderableSeries1.DataSeries = dataSeries1;
            lineRenderableSeries2.DataSeries = dataSeries2;
        }

        public static void AppendData((int, int) tuple1, (int, int) tuple2)
        {
            AppendDataToSeries(dataSeries1, tuple1);
            AppendDataToSeries(dataSeries2, tuple2);

            // Trigger the event with the latest SPO2 value
            OnSpo2Updated?.Invoke(tuple2.Item2);
            OnHRUpdated?.Invoke(tuple1.Item2);
        }

        private static void AppendDataToSeries(XyDataSeries<int, int> series, (int, int) tuple)
        {
            series.Append(tuple.Item1, tuple.Item2);
            Trace.WriteLine($"PLOTTED: {tuple.Item1}, {tuple.Item2}");
        }

        public static void ClearData()
        {
            dataSeries1.Clear();
            dataSeries2.Clear();
        }
    }
}
