using Infrastructure.Serialization;
using Infrastructure.Tools;
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Infrastructure.Navigation.EulerCalculations
{
    public class EulerAngles
    {
        public double Yaw { get; set; }
        public double Pitch { get; set; }
        public double Roll { get; set; }

        public static int BinLength
        {
            get
            {
                return 3 * sizeof(UInt32);
            }
        }

        public bool IsClear => (Yaw == 0) && (Pitch == 0) && (Roll == 0);

        public void Clear()
        {
            Yaw = Pitch = Roll = 0.0;
        }

        public EulerAngles Clone()
        {
            return (EulerAngles)this.MemberwiseClone();
        }


        public EulerAngles()
        {
            Clear();
        }

        public EulerAngles(double y, double p, double r)
        {
            Yaw = y;
            Pitch = p;
            Roll = r;
        }

        public void ConvertToRadians()
        {
            Yaw *= Constants.DegToRadRatio;
            Pitch *= Constants.DegToRadRatio;
            Roll *= Constants.DegToRadRatio;
        }

        public void ConvertToDegrees()
        {
            Yaw *= Constants.RadToDegRatio;
            Pitch *= Constants.RadToDegRatio;
            Roll *= Constants.RadToDegRatio;
        }
        /// <summary>
        /// Decode Constructor
        /// </summary>
        /// <param name="reader"></param>
        public EulerAngles(BinaryReader reader, Coding type = Coding.Angle)
        {
            switch (type)
            {
                case Coding.Angle:
                    Yaw = CommunicationBase.DecodeAngle(reader);
                    Pitch = CommunicationBase.DecodeAngle(reader);
                    Roll = CommunicationBase.DecodeAngle(reader);
                    break;
                case Coding.Motion:
                    Yaw = CommunicationBase.DecodeMotion(reader);
                    Pitch = CommunicationBase.DecodeMotion(reader);
                    Roll = CommunicationBase.DecodeMotion(reader);
                    break;
                case Coding.General:
                    Yaw = CommunicationBase.DecodeGeneralDoubleValue(reader);
                    Pitch = CommunicationBase.DecodeGeneralDoubleValue(reader);
                    Roll = CommunicationBase.DecodeGeneralDoubleValue(reader);
                    break;
                default:
                    break;
            }
        }

        public void Encode(BinaryWriter writer, Coding type = Coding.Angle)
        {
            switch (type)
            {
                case Coding.Angle:
                    writer.Write(CommunicationBase.EncodeAngle(Yaw));
                    writer.Write(CommunicationBase.EncodeAngle(Pitch));
                    writer.Write(CommunicationBase.EncodeAngle(Roll));
                    break;
                case Coding.Motion:
                    writer.Write(CommunicationBase.EncodeMotion(Yaw));
                    writer.Write(CommunicationBase.EncodeMotion(Pitch));
                    writer.Write(CommunicationBase.EncodeMotion(Roll));
                    break;
                case Coding.General:
                    writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Yaw));
                    writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Pitch));
                    writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Roll));
                    break;
                default:
                    break;
            }
        }

        [OnSerializing]
        private void OnJsonSerializing(StreamingContext context)
        {
            Yaw = MathTools.Cyclic2PI(Yaw) * Constants.RadToDegRatio;
            Pitch = MathTools.Cyclic2PI(Pitch) * Constants.RadToDegRatio;
            Roll = MathTools.Cyclic2PI(Roll) * Constants.RadToDegRatio;
        }

        [OnSerialized]
        private void OnJsonSerialized(StreamingContext context)
        {
            ConvertToRadians();
        }

        [OnDeserialized]
        private void OnJsonDeserialized(StreamingContext context)
        {
            ConvertToRadians();
        }


        public override bool Equals(object obj)
        {
            if (obj is EulerAngles checkedObject)
            {
                return checkedObject.Yaw == this.Yaw && checkedObject.Pitch == this.Pitch && checkedObject.Roll == this.Roll;
            }
            else return false;
        }

        public override string ToString()
        {
            string result = "";
            result += String.Format("Yaw: {0:0.000}, ", Yaw * Constants.RadToDegRatio);
            result += String.Format("Pitch: {0:0.000}, ", Pitch * Constants.RadToDegRatio);
            result += String.Format("Roll: {0:0.000}", Roll * Constants.RadToDegRatio);
            return result;
        }

    }
}
