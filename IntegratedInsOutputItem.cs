using Infrastructure.FileManagement.DataRecording;
using Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput;
using System;
using System.Collections.Generic;

namespace RecordDecoderPro.ItemTemplates
{
    internal sealed class IntegratedInsOutputItem : RecordTypeItem
    {
        #region Properties

        public IntegratedInsOutputDictionary OutputDict { get; private set; }

        #endregion

        #region Columns

        public static readonly string[] IntegratedInsOutputColumns =
        {
            "RcvTime[hms]", "RcvTime[sec]",

            "OutputTimeDeviceCode", "OutputTimeDeviceId", "OutputTime[hms]", "OutputTime[sec]", 

            "PositionLatDeviceCode", "PositionLatDeviceId", "PositionLatValue",
            "PositionLonDeviceCode", "PositionLonDeviceId", "PositionLonValue",
            "PositionAltDeviceCode", "PositionAltDeviceId", "PositionAltValue",

            "EulerRollDeviceCode", "EulerRollDeviceId", "EulerRollValue",
            "EulerPitchDeviceCode", "EulerPitchDeviceId", "EulerPitchValue",
            "EulerAzimuthDeviceCode", "EulerAzimuthDeviceId", "EulerAzimuthValue",

            "EulerRollRateDeviceCode", "EulerRollRateDeviceId", "EulerRollRateValue",
            "EulerPitchRateDeviceCode", "EulerPitchRateDeviceId", "EulerPitchRateValue",
            "EulerAzimuthRateDeviceCode", "EulerAzimuthRateDeviceId", "EulerAzimuthRateValue",

            "VelocityTotalDeviceCode", "VelocityTotalDeviceId", "VelocityTotalValue",
            "VelocityNorthDeviceCode", "VelocityNorthDeviceId", "VelocityNorthValue",
            "VelocityEastDeviceCode", "VelocityEastDeviceId", "VelocityEastValue",
            "VelocityDownDeviceCode", "VelocityDownDeviceId", "VelocityDownValue",

            "StatusDeviceCode", "StatusDeviceId", "StatusValue",
            "CourseDeviceCode", "CourseDeviceId", "CourseValue",
        };

        #endregion

        #region Constructors

        public IntegratedInsOutputItem() : base()
        {
            ColumnsNames = IntegratedInsOutputColumns;
        }

        #endregion

        #region Functions

        // Initializes item dictionary from raw binary payload
        public void InitializeDict(DataRecordHeader i_Header, byte[] i_RawData)
        {
            OutputDict = new IntegratedInsOutputDictionary(i_Header, i_RawData);
            dict = OutputDict.Dictionary;
        }

        #endregion

        #region Nested Types

        internal sealed class IntegratedInsOutputDictionary
        {
            #region Properties

            public Dictionary<string, string> Dictionary { get; }

            #endregion

            #region Constructors

            public IntegratedInsOutputDictionary(DataRecordHeader i_Header, byte[] i_RawData)
            {
                Dictionary = new Dictionary<string, string>();
                Process(i_Header, i_RawData);
            }

            #endregion

            #region Private Functions

            // Decodes integrated output using a CommFrame and formats values into CSV-ready columns
            private void Process(DataRecordHeader i_Header, byte[] i_RawData)
            {
                int i = 0;

                IntegratedInsOutputCommFrame frame = new IntegratedInsOutputCommFrame();
                frame.DecodeBinaryData(i_RawData, i_Header.DataLength);

                DateTime rcvTime = DateTime.FromBinary(i_Header.Time);
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{rcvTime:HH:mm:ss.fff},");
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{rcvTime.TimeOfDay.TotalSeconds:F4},");

                DateTime outputTime = frame.Data.OutputTimeUtc;
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{outputTime:HH:mm:ss.fff},");
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{outputTime.TimeOfDay.TotalSeconds:F4},");
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{frame.Data.OutputTimeDeviceCode},");
                Dictionary.Add(IntegratedInsOutputColumns[i++], $"{frame.Data.OutputTimeDeviceId},");

                AddTriplet(ref i, frame.Data.PositionLat);
                AddTriplet(ref i, frame.Data.PositionLon);
                AddTriplet(ref i, frame.Data.PositionAlt);

                AddTriplet(ref i, frame.Data.EulerRoll);
                AddTriplet(ref i, frame.Data.EulerPitch);
                AddTriplet(ref i, frame.Data.EulerAzimuth);

                AddTriplet(ref i, frame.Data.EulerRollRate);
                AddTriplet(ref i, frame.Data.EulerPitchRate);
                AddTriplet(ref i, frame.Data.EulerAzimuthRate);

                AddTriplet(ref i, frame.Data.VelocityTotal);
                AddTriplet(ref i, frame.Data.VelocityNorth);
                AddTriplet(ref i, frame.Data.VelocityEast);
                AddTriplet(ref i, frame.Data.VelocityDown);

                AddTriplet(ref i, frame.Data.Status);
                AddTriplet(ref i, frame.Data.Course);
            }

            // Adds DeviceCode + DeviceId + Value using the current column index
            private void AddTriplet(ref int io_Index, IntegratedValueTriplet i_Triplet)
            {
                Dictionary.Add(IntegratedInsOutputColumns[io_Index++], $"{i_Triplet.DeviceCode},");
                Dictionary.Add(IntegratedInsOutputColumns[io_Index++], $"{i_Triplet.DeviceId},");
                Dictionary.Add(IntegratedInsOutputColumns[io_Index++], $"{i_Triplet.Value:F8},");
            }

            #endregion
        }

        #endregion
    }
}
