using System;

namespace Arlanet.CopernicaNET.Interfaces.Data
{
    public interface ICopernicaDataItem
    {
		int ID { get; set; }

        int DatabaseId { get; }
    }
}
