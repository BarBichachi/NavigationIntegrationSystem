namespace Infrastructure.Navigation.NavigationSystems.IntegratedInsOutput
{
    public struct IntegratedValueTriplet
    {
        public ushort DeviceCode { get; }
        public ushort DeviceId { get; }
        public double Value { get; }

        public IntegratedValueTriplet(ushort i_DeviceCode, ushort i_DeviceId, double i_Value)
        {
            DeviceCode = i_DeviceCode;
            DeviceId = i_DeviceId;
            Value = i_Value;
        }
    }
}