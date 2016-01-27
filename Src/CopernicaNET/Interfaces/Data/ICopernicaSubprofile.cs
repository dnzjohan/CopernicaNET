using System;

namespace Arlanet.CopernicaNET.Interfaces.Data
{
    public interface ICopernicaSubprofile: ICopernicaDataItem
    {
        int CollectionId { get; }
	    int ProfileId { get; set; }
    }
}
