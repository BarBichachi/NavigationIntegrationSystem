using Infrastructure.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Navigation
{
    public class NEDData
    {
        public double North { get; set; }
        public double East { get; set; }
        public double Down { get; set; }

        public int Length
        {
            get
            {
                return 3 * sizeof(double);
            }
        }

        public static int BinLength
        {
            get
            {
                return 3 * sizeof(Int32);
            }
        }

        public NEDData(BinaryReader reader)
        {
            North = CommunicationBase.DecodeGeneralDoubleValue(reader);
            East = CommunicationBase.DecodeGeneralDoubleValue(reader);
            Down = CommunicationBase.DecodeGeneralDoubleValue(reader);
        }

        public NEDData()
        {
            Clear();
        }

        public void Clear()
        {
            North = 0;
            East = 0;
            Down = 0;
        }

        public void Encode(BinaryWriter writer)
        {
            writer.Write(CommunicationBase.EncodeGeneralDoubleValue(North));
            writer.Write(CommunicationBase.EncodeGeneralDoubleValue(East));
            writer.Write(CommunicationBase.EncodeGeneralDoubleValue(Down));
        }


        public NEDData Clone()
        {
            return (NEDData)MemberwiseClone();
        }


        public override bool Equals(object obj)
        {
            if (obj is NEDData checkedObject)
            {
                return checkedObject.North == this.North &&
                       checkedObject.East == this.East &&
                       checkedObject.Down == this.Down;
            }
            else return false;
        }

        public override string ToString()
        {
            string result = "";
            result += String.Format("North: {0:0.000}, ", North);
            result += String.Format("East: {0:0.000}, ", East);
            result += String.Format("Down: {0:0.000}", Down);
            return result;
        }
    }
}
