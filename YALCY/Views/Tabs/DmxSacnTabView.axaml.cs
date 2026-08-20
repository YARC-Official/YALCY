using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Linq;

namespace YALCY.Views.Tabs;

public partial class DmxSacnTabView : UserControl
{
    public DmxSacnTabView()
    {
        InitializeComponent();
        this.AttachedToVisualTree += (s, e) => UpdateLayoutState(this.Bounds.Width);
        this.SizeChanged += DmxSacnTabView_SizeChanged;
    }

    private void DmxSacnTabView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLayoutState(e.NewSize.Width);
    }

    private void UpdateLayoutState(double width)
    {
        if (NetworkSettingsGrid != null && NetworkAdapterCard != null && BroadcastUniverseCard != null && StrobeBehaviorCard != null)
        {
            if (width < 600)
            {
                // Ultra narrow: 3 stacked rows
                NetworkSettingsGrid.ColumnDefinitions.Clear();
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                
                NetworkSettingsGrid.RowDefinitions.Clear();
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetRow(NetworkAdapterCard, 0);
                Grid.SetColumn(NetworkAdapterCard, 0);
                Grid.SetColumnSpan(NetworkAdapterCard, 1);

                Grid.SetRow(BroadcastUniverseCard, 1);
                Grid.SetColumn(BroadcastUniverseCard, 0);
                Grid.SetColumnSpan(BroadcastUniverseCard, 1);

                Grid.SetRow(StrobeBehaviorCard, 2);
                Grid.SetColumn(StrobeBehaviorCard, 0);
                Grid.SetColumnSpan(StrobeBehaviorCard, 1);
            }
            else if (width < 1050)
            {
                // Compact mode: Row 0 has Adapter + Universe together, Row 1 has Strobe spanning full width
                NetworkSettingsGrid.ColumnDefinitions.Clear();
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                
                NetworkSettingsGrid.RowDefinitions.Clear();
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetRow(NetworkAdapterCard, 0);
                Grid.SetColumn(NetworkAdapterCard, 0);
                Grid.SetColumnSpan(NetworkAdapterCard, 1);

                Grid.SetRow(BroadcastUniverseCard, 0);
                Grid.SetColumn(BroadcastUniverseCard, 1);
                Grid.SetColumnSpan(BroadcastUniverseCard, 1);

                Grid.SetRow(StrobeBehaviorCard, 1);
                Grid.SetColumn(StrobeBehaviorCard, 0);
                Grid.SetColumnSpan(StrobeBehaviorCard, 2);
            }
            else
            {
                // Wide mode: Row 0 has all 3 in 1 row side-by-side
                NetworkSettingsGrid.ColumnDefinitions.Clear();
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                NetworkSettingsGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));

                NetworkSettingsGrid.RowDefinitions.Clear();
                NetworkSettingsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                Grid.SetRow(NetworkAdapterCard, 0);
                Grid.SetColumn(NetworkAdapterCard, 0);
                Grid.SetColumnSpan(NetworkAdapterCard, 1);

                Grid.SetRow(BroadcastUniverseCard, 0);
                Grid.SetColumn(BroadcastUniverseCard, 1);
                Grid.SetColumnSpan(BroadcastUniverseCard, 1);

                Grid.SetRow(StrobeBehaviorCard, 0);
                Grid.SetColumn(StrobeBehaviorCard, 2);
                Grid.SetColumnSpan(StrobeBehaviorCard, 1);
            }
        }

        if (InstrumentNotesGrid != null)
        {
            if (width < 650)
            {
                InstrumentNotesGrid.Columns = 1;
            }
            else if (width < 1050)
            {
                InstrumentNotesGrid.Columns = 2;
            }
            else
            {
                InstrumentNotesGrid.Columns = 4;
            }
        }

        if (AdvancedSettingsGrid != null)
        {
            if (width < 650)
            {
                AdvancedSettingsGrid.Columns = 1;
            }
            else if (width < 1050)
            {
                AdvancedSettingsGrid.Columns = 2;
            }
            else
            {
                AdvancedSettingsGrid.Columns = 4;
            }
        }

        bool isCompact = width < 1200;

        foreach (var grid in this.GetVisualDescendants().OfType<Grid>())
        {
            if (grid.Classes.Contains("DimmerRowContainer") || grid.Classes.Contains("ColorRowContainer"))
            {
                if (grid.Children.Count >= 2)
                {
                    if (isCompact)
                    {
                        grid.ColumnDefinitions.Clear();
                        grid.RowDefinitions.Clear();
                        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                        Grid.SetColumn(grid.Children[0], 0);
                        Grid.SetRow(grid.Children[0], 0);
                        Grid.SetColumn(grid.Children[1], 0);
                        Grid.SetRow(grid.Children[1], 1);
                        if (grid.Children[1] is Control c)
                        {
                            c.Margin = new Thickness(0, 10, 0, 0);
                        }
                    }
                    else
                    {
                        grid.ColumnDefinitions.Clear();
                        grid.RowDefinitions.Clear();
                        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
                        Grid.SetColumn(grid.Children[0], 0);
                        Grid.SetRow(grid.Children[0], 0);
                        Grid.SetColumn(grid.Children[1], 1);
                        Grid.SetRow(grid.Children[1], 0);
                        if (grid.Children[1] is Control c)
                        {
                            c.Margin = new Thickness(0);
                        }
                    }
                }
            }
        }

        foreach (var tb in this.GetVisualDescendants().OfType<TextBlock>())
        {
            if (tb.Classes.Contains("WideHeader"))
            {
                tb.IsVisible = !isCompact;
            }
            else if (tb.Classes.Contains("CompactHeader"))
            {
                tb.IsVisible = isCompact;
            }
        }
    }
}
