using System.Collections.Concurrent;
using System.Collections.Generic;

namespace VaxDrive.VaxAgent.Network;

public class TopologyMap
{
    private readonly ConcurrentDictionary<string, DeviceRecord> _assets = new();

    public IEnumerable<DeviceRecord> GetAssets()
    {
        return _assets.Values;
    }

    public void Upsert(DeviceRecord record)
    {
        _assets.AddOrUpdate(record.MacAddress, record, (_, _) => record);
    }
}
