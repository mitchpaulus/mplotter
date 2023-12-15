using System.Collections.Generic;

namespace csvplot;

public class TrendDialogVm
{
    public TrendDialogVm(List<IDataSource> sources)
    {
        Sources = sources;
    }

    public List<DataSourceViewModel> Sources { get; }
}