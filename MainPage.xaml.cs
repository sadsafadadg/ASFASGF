using MortgageCalc.Models;

namespace MortgageCalc;

public partial class MainPage : ContentPage
{
    private CalcResult? _lastResult;
    private bool _isYearlyView;
    private int _earlyRowCounter = 0;

    public MainPage()
    {
        InitializeComponent();
        LayoutEarlyToggle(false);
    }

    // ======== 提前还款行管理 ========

    private void OnEarlyToggled(object? sender, ToggledEventArgs e)
    {
        LayoutEarlyToggle(e.Value);
        if (e.Value && EarlyList.Children.Count == 0)
            AddEarlyRow(60, 100000, 0);
    }

    private void LayoutEarlyToggle(bool enabled)
    {
        EarlyList.IsVisible = enabled;
        AddEarlyBtn.IsVisible = enabled;
    }

    private void OnAddEarlyClicked(object? sender, EventArgs e)
    {
        // 默认值从上一行推算
        int lastMonth = 60;
        if (EarlyList.Children.Count > 0)
        {
            var last = EarlyList.Children[^1] as View;
            if (last?.BindingContext is EarlyRowData data)
                lastMonth = data.Month + 60;
        }
        AddEarlyRow(Math.Min(lastMonth, 360), 100000, 0);
    }

    private void AddEarlyRow(int month, double amount, int method)
    {
        var data = new EarlyRowData
        {
            Index = _earlyRowCounter++,
            Month = month,
            Amount = amount,
            Method = method
        };

        var monthEntry = new Entry
        {
            Text = month.ToString(),
            Keyboard = Keyboard.Numeric,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.End,
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            WidthRequest = 60
        };
        monthEntry.TextChanged += (_, _) =>
        {
            if (int.TryParse(monthEntry.Text, out int m)) data.Month = m;
        };

        var amountEntry = new Entry
        {
            Text = amount.ToString(),
            Keyboard = Keyboard.Numeric,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.End,
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            WidthRequest = 80
        };
        amountEntry.TextChanged += (_, _) =>
        {
            if (double.TryParse(amountEntry.Text, out double a)) data.Amount = a;
        };

        var rbGroup = Guid.NewGuid().ToString();

        var rbReduceTerm = new RadioButton
        {
            Content = "缩短年限", GroupName = rbGroup, IsChecked = method == 0,
            FontSize = 12
        };
        rbReduceTerm.CheckedChanged += (_, e) => { if (e.Value) { data.Method = 0; amountEntry.IsEnabled = true; } };

        var rbReducePay = new RadioButton
        {
            Content = "减少月供", GroupName = rbGroup, IsChecked = method == 1,
            FontSize = 12
        };
        rbReducePay.CheckedChanged += (_, e) => { if (e.Value) { data.Method = 1; amountEntry.IsEnabled = true; } };

        var rbPayOff = new RadioButton
        {
            Content = "还清", GroupName = rbGroup, IsChecked = method == 2,
            FontSize = 12
        };
        rbPayOff.CheckedChanged += (_, e) => { if (e.Value) { data.Method = 2; amountEntry.IsEnabled = false; } };

        var deleteBtn = new Button
        {
            Text = "✕",
            FontSize = 14,
            TextColor = Color.FromArgb("#E63946"),
            BackgroundColor = Colors.Transparent,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = new Thickness(0),
            CornerRadius = 18
        };

        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#F8F9FA"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Color.FromArgb("#DADCE0"),
            Padding = new Thickness(10, 8),
            BindingContext = data
        };

        var row = new VerticalStackLayout { Spacing = 6 };

        // Row 1: 第 [  ] 个月 还款 [  ] 元  [✕]
        var topRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 4
        };
        topRow.Add(new Label { Text = "第", VerticalOptions = LayoutOptions.Center, FontSize = 13 });
        topRow.Add(monthEntry, 1);
        topRow.Add(new Label { Text = "个月 还款", VerticalOptions = LayoutOptions.Center, FontSize = 13, Margin = new Thickness(4, 0) }, 2);
        topRow.Add(amountEntry, 3);
        topRow.Add(new Label { Text = "元", VerticalOptions = LayoutOptions.Center, FontSize = 13, Margin = new Thickness(2, 0, 0, 0) }, 4);
        topRow.Add(deleteBtn, 5);

        // Row 2: 缩短年限  减少月供  还清
        var methodRow = new HorizontalStackLayout { Spacing = 6 };
        methodRow.Add(rbReduceTerm);
        methodRow.Add(rbReducePay);
        methodRow.Add(rbPayOff);

        row.Add(topRow);
        row.Add(methodRow);
        border.Content = row;
        EarlyList.Children.Add(border);

        deleteBtn.Clicked += (_, _) =>
        {
            EarlyList.Children.Remove(border);
        };
    }

    // ======== 计算 ========

    private bool IsEqualPrincipal => MethodEqualPrincipal.IsChecked;

    private void OnCalculateClicked(object? sender, EventArgs e) => DoCalculate();

    private void DoCalculate()
    {
        if (!double.TryParse(LoanAmount.Text, out double loanAmount) || loanAmount <= 0)
        { ShowToast("请输入有效的贷款总额"); LoanAmount.Focus(); return; }
        if (!double.TryParse(AnnualRate.Text, out double annualRate) || annualRate <= 0)
        { ShowToast("请输入有效的年利率"); AnnualRate.Focus(); return; }
        if (!int.TryParse(LoanYears.Text, out int years) || years <= 0)
        { ShowToast("请输入有效的贷款年限"); LoanYears.Focus(); return; }

        var earlyRepayments = new List<EarlyRepayment>();
        if (EarlySwitch.IsToggled)
        {
            foreach (var child in EarlyList.Children)
            {
                if (child is Border b && b.BindingContext is EarlyRowData d)
                {
                    if (d.Month <= 0)
                    { ShowToast("请填写有效的提前还款月份"); return; }
                    if (d.Method != 2 && d.Amount <= 0)
                    { ShowToast("请填写有效的提前还款金额"); return; }
                    if (d.Month >= years * 12)
                    { ShowToast("提前还款月份不能超过贷款总期数"); return; }

                    earlyRepayments.Add(new EarlyRepayment
                    {
                        Month = d.Month,
                        Amount = d.Amount,
                        Method = d.Method
                    });
                }
            }
        }

        var result = Calculator.Calculate(
            loanAmount, annualRate, years, IsEqualPrincipal,
            earlyRepayments.Count > 0 ? earlyRepayments : null!);

        if (result.HasError)
        { ShowToast(result.Error!); return; }

        _lastResult = result;
        ShowResults(result);
    }

    private void ShowResults(CalcResult result)
    {
        ResultMonthlyPayment.Text = result.MonthlyPaymentText;
        ResultTotalPayment.Text = $"{result.TotalPayment:N0}";
        ResultTotalInterest.Text = result.TotalInterestText;
        ResultSavedInterest.Text = result.SavedInterest > 0 ? $"{result.SavedInterest:N0}" : "—";

        if (result.EarlyApplied)
        {
            ResultEarlyNote.Text = result.EarlyNote;
            ResultEarlyNote.IsVisible = true;
        }
        else
        {
            ResultEarlyNote.IsVisible = false;
        }

        ResultCard.IsVisible = true;
        RenderTable(result, _isYearlyView);
    }

    private void RenderTable(CalcResult result, bool yearly)
    {
        var items = new List<ScheduleRow>();

        if (yearly)
        {
            int yearCount = (result.TotalMonths + 11) / 12;
            for (int y = 0; y < yearCount; y++)
            {
                int start = y * 12;
                int end = Math.Min(start + 12, result.TotalMonths);
                double yrP = 0, yrI = 0, yrB = 0;
                for (int m = start; m < end; m++)
                { yrP += result.Principals[m]; yrI += result.Interests[m]; yrB = result.Balances[m]; }
                bool isEarly = result.IsEarlyMonth(start);
                items.Add(new ScheduleRow
                {
                    Period = $"{y + 1}年", Payment = $"{yrP + yrI:N0}",
                    Principal = $"{yrP:N0}", Interest = $"{yrI:N0}",
                    Balance = $"{yrB:N0}",
                    PeriodColor = isEarly ? Colors.Green : Colors.Black
                });
            }
            SetHeaders("年份", "年供");
        }
        else
        {
            for (int i = 0; i < result.TotalMonths; i++)
            {
                bool isEarly = result.IsEarlyMonth(i);
                items.Add(new ScheduleRow
                {
                    Period = $"{i + 1}", Payment = $"{result.Principals[i] + result.Interests[i]:N0}",
                    Principal = $"{result.Principals[i]:N0}", Interest = $"{result.Interests[i]:N0}",
                    Balance = $"{result.Balances[i]:N0}",
                    PeriodColor = isEarly ? Colors.Green : Colors.Black
                });
            }
            SetHeaders("期数", "月供");
        }

        ResultTable.ItemsSource = items;
    }

    private void SetHeaders(string col0, string col1)
    {
        var labels = TableHeader.Children.OfType<Label>().ToArray();
        if (labels.Length >= 2) { labels[0].Text = col0; labels[1].Text = col1; }
    }

    private void OnViewMonthlyClicked(object? sender, EventArgs e)
    {
        _isYearlyView = false;
        ViewMonthly.BackgroundColor = Colors.White;
        ViewMonthly.TextColor = Color.FromArgb("#1A73E8");
        ViewYearly.BackgroundColor = Colors.Transparent;
        ViewYearly.TextColor = Color.FromArgb("#5F6368");
        if (_lastResult != null) RenderTable(_lastResult, false);
    }

    private void OnViewYearlyClicked(object? sender, EventArgs e)
    {
        _isYearlyView = true;
        ViewYearly.BackgroundColor = Colors.White;
        ViewYearly.TextColor = Color.FromArgb("#1A73E8");
        ViewMonthly.BackgroundColor = Colors.Transparent;
        ViewMonthly.TextColor = Color.FromArgb("#5F6368");
        if (_lastResult != null) RenderTable(_lastResult, true);
    }

    private async void ShowToast(string message)
    {
        await DisplayAlert("", message, "确定");
    }
}

// 提前还款行数据（BindingContext用）
public class EarlyRowData
{
    public int Index { get; set; }
    public int Month { get; set; } = 60;
    public double Amount { get; set; } = 100000;
    public int Method { get; set; }
}

public class ScheduleRow
{
    public string Period { get; set; } = "";
    public string Payment { get; set; } = "";
    public string Principal { get; set; } = "";
    public string Interest { get; set; } = "";
    public string Balance { get; set; } = "";
    public string Note { get; set; } = "";
    public Color PeriodColor { get; set; } = Colors.Black;
}
