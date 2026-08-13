using System.Windows;
using MoreWaterObjectSpy.Core;

namespace MoreWaterObjectSpy.Views;

public partial class LocatorPickerWindow : Wpf.Ui.Controls.FluentWindow
{
    public string? SelectedLocator { get; private set; }
    public LocatorCandidate? SelectedCandidate { get; private set; }

    public LocatorPickerWindow(CapturedObject obj)
    {
        InitializeComponent();
        LblObjeto.Text = "Elemento:  " + obj.DisplayName;
        foreach (var c in obj.Candidates) LstCand.Items.Add(c);
        if (LstCand.Items.Count > 0) LstCand.SelectedIndex = 0;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (LstCand.SelectedItem is LocatorCandidate c) { SelectedLocator = c.Locator; SelectedCandidate = c; }
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
