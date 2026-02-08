using Infrastructure.Serialization;

using System.IO;

namespace Infrastructure.Navigation.EulerCalculations
{
    public class EulerData
    {
        public EulerAngles Angles { get; set; }
        public EulerAngles Rates { get; set; }

        public EulerAngles Angles_Deg
        {
            get
            {
                EulerAngles output = new EulerAngles();
                output = Angles.Clone();
                output.ConvertToDegrees();
                return output;
            }

            set
            {
                value.ConvertToRadians();
                Angles = value.Clone();
            }
        }

        public EulerAngles Rates_Deg
        {
            get
            {
                EulerAngles output = new EulerAngles(); ;
                output = Rates.Clone();
                output.ConvertToDegrees();
                return output;
            }

            set
            {
                value.ConvertToRadians();
                Rates = value.Clone();
            }
        }

        public bool IsClear => (Angles.IsClear) && (Rates.IsClear);

        public void Clear()
        {
            Angles.Clear();
            Rates.Clear();
        }

        public EulerData Clone()
        {
            EulerData outValue = new EulerData();
            outValue.Angles = this.Angles.Clone();
            outValue.Rates = this.Rates.Clone();
            return outValue;
        }

        public int Length
        {
            get
            {
                return 6 * sizeof(double);
            }
        }

        public static int BinLength
        {
            get
            {
                return 2 * EulerAngles.BinLength;
            }
        }

        public EulerData()
        {
            Angles = new EulerAngles();
            Rates = new EulerAngles();
            Clear();
        }

        public EulerData(EulerAngles angles, EulerAngles rates)
        {
            Angles = angles.Clone();
            Rates = rates.Clone();
        }

        public void ConvertToRadians()
        {
            Angles.ConvertToRadians();
            Rates.ConvertToRadians();
        }

        public void ConvertToDegrees()
        {
            Angles.ConvertToDegrees();
            Rates.ConvertToDegrees();
        }

        /// <summary>
        /// Decode Constructor
        /// </summary>
        /// <param name="reader"></param>
        public EulerData(BinaryReader reader)
        {
            Angles = new EulerAngles(reader, Coding.Angle);
            Rates = new EulerAngles(reader, Coding.Motion);
        }

        public void Encode(BinaryWriter writer)
        {
            Angles.Encode(writer, Coding.Angle);
            Rates.Encode(writer, Coding.Motion);
        }
    }
}

