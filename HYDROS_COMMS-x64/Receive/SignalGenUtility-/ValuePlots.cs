using SciChart.Charting.Visuals;
using SciChart.Charting.Model.DataSeries.Heatmap2DArrayDataSeries;
using SciChart.Charting.Visuals.RenderableSeries;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SciChart.Charting.Model.DataSeries;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using SciChart.Charting.Visuals.Axes;
using SciChart.Data.Model;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using DelsysNICommon;

namespace DelsysSigNIalGen
{
    public class ValuePlot // Simple Line Plots added. One for hr, one for spo2
    {
        private static XyDataSeries<int, int> dataSeries1;
        private static XyDataSeries<int, int> dataSeries2;


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
        }

        private static void AppendDataToSeries(XyDataSeries<int, int> series, (int, int) tuple)
        {
            series.Append(tuple.Item1, tuple.Item2); // call, val
            Trace.WriteLine($"PLOTTED: {tuple.Item1}, {tuple.Item2}");
        }

        public static void ClearData()
        {
            dataSeries1.Clear();
            dataSeries2.Clear();
        }
    }
}


