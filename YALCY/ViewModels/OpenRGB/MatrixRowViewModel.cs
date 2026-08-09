using System.Collections.ObjectModel;

namespace YALCY.ViewModels.OpenRGB;

public class MatrixRowViewModel
{
    public uint RowIndex { get; }
    public ObservableCollection<MatrixCellViewModel> Cells { get; } = new();

    public MatrixRowViewModel(uint rowIndex)
    {
        RowIndex = rowIndex;
    }
}
