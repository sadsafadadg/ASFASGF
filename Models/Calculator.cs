namespace MortgageCalc.Models;

public static class Calculator
{
    public static double CalcEqualMonthlyPayment(double amount, double monthlyRate, int months)
    {
        if (monthlyRate == 0) return amount / months;
        double factor = Math.Pow(1 + monthlyRate, months);
        return amount * monthlyRate * factor / (factor - 1);
    }

    public static int CalcRemainingMonths(double balance, double monthlyRate, double monthlyPayment)
    {
        if (monthlyPayment <= balance * monthlyRate) return int.MaxValue;
        if (monthlyRate == 0) return (int)Math.Ceiling(balance / monthlyPayment);
        double ratio = monthlyPayment / (monthlyPayment - balance * monthlyRate);
        int n = (int)Math.Ceiling(Math.Log(ratio) / Math.Log(1 + monthlyRate));
        return Math.Max(1, n);
    }

    public static ScheduleResult CalcOriginalSchedule(double loanAmount, double monthlyRate, int months, bool isEqualPrincipal)
    {
        var principals = new double[months];
        var interests = new double[months];
        var balances = new double[months];

        if (!isEqualPrincipal)
        {
            double mp = CalcEqualMonthlyPayment(loanAmount, monthlyRate, months);
            double bal = loanAmount;
            for (int i = 0; i < months; i++)
            {
                double interest = bal * monthlyRate;
                double principal = mp - interest;
                bal -= principal;
                principals[i] = principal;
                interests[i] = interest;
                balances[i] = bal;
            }
        }
        else
        {
            double monthlyPrincipal = loanAmount / months;
            double bal = loanAmount;
            for (int i = 0; i < months; i++)
            {
                double interest = bal * monthlyRate;
                bal -= monthlyPrincipal;
                principals[i] = monthlyPrincipal;
                interests[i] = interest;
                balances[i] = bal;
            }
        }
        return new ScheduleResult(principals, interests, balances);
    }

    // 主计算（支持多笔提前还款）
    public static CalcResult Calculate(
        double loanAmount, double annualRate, int years,
        bool isEqualPrincipal,
        List<EarlyRepayment> earlyRepayments)
    {
        int totalMonths = years * 12;
        double monthlyRate = annualRate / 100.0 / 12.0;

        var orig = CalcOriginalSchedule(loanAmount, monthlyRate, totalMonths, isEqualPrincipal);
        double origTotalInterest = orig.Interests.Sum();

        // 当前还款计划（逐步更新）
        double[] curPrincipals = orig.Principals;
        double[] curInterests = orig.Interests;
        double[] curBalances = orig.Balances;
        int curMonths = totalMonths;
        List<string> notes = new();
        bool earlyApplied = false;

        // 当前月供参数（等额本息跟踪月供，等额本金跟踪每月本金）
        double curMonthlyPayment = isEqualPrincipal
            ? loanAmount / totalMonths
            : CalcEqualMonthlyPayment(loanAmount, monthlyRate, totalMonths);

        if (earlyRepayments != null && earlyRepayments.Count > 0)
        {
            var sorted = earlyRepayments.OrderBy(er => er.Month).ToList();

            foreach (var er in sorted)
            {
                if (er.Month > curMonths) continue; // 已缩短到此月之后，跳过

                double erAmount = er.Method == 2 ? curBalances[er.Month - 1] : er.Amount;
                double remainingBalance = curBalances[er.Month - 1] - erAmount;

                if (remainingBalance <= 0)
                {
                    // 还清
                    curMonths = er.Month;
                    curPrincipals = curPrincipals[..curMonths];
                    curInterests = curInterests[..curMonths];
                    curBalances = curBalances[..curMonths];
                    curPrincipals[curMonths - 1] = curPrincipals[curMonths - 1] + erAmount;
                    curBalances[curMonths - 1] = 0;
                    earlyApplied = true;
                    notes.Add(er.Method == 2 ? $"第{er.Month}个月一次性还清" : $"第{er.Month}个月还清");
                    break;
                }

                earlyApplied = true;

                if (!isEqualPrincipal)
                {
                    // ============ 等额本息 ============
                    if (er.Method == 0) // 缩短年限
                    {
                        int remainMonths = CalcRemainingMonths(remainingBalance, monthlyRate, curMonthlyPayment);
                        int newTotal = er.Month + remainMonths;

                        var np = new double[newTotal];
                        var ni = new double[newTotal];
                        var nb = new double[newTotal];

                        Array.Copy(curPrincipals, np, er.Month);
                        Array.Copy(curInterests, ni, er.Month);
                        Array.Copy(curBalances, nb, er.Month);
                        np[er.Month - 1] += erAmount;

                        double bal = remainingBalance;
                        for (int i = 0; i < remainMonths; i++)
                        {
                            double interest = bal * monthlyRate;
                            double principal = curMonthlyPayment - interest;
                            if (principal > bal) principal = bal;
                            bal -= principal;
                            np[er.Month + i] = principal;
                            ni[er.Month + i] = interest;
                            nb[er.Month + i] = bal;
                        }

                        curPrincipals = np;
                        curInterests = ni;
                        curBalances = nb;
                        int shortened = curMonths - newTotal;
                        curMonths = newTotal;
                        notes.Add($"第{er.Month}个月后缩短{shortened}个月");
                    }
                    else // 减少月供
                    {
                        int remainMonths = curMonths - er.Month;
                        curMonthlyPayment = CalcEqualMonthlyPayment(remainingBalance, monthlyRate, remainMonths);

                        var np = new double[curMonths];
                        var ni = new double[curMonths];
                        var nb = new double[curMonths];

                        Array.Copy(curPrincipals, np, er.Month);
                        Array.Copy(curInterests, ni, er.Month);
                        Array.Copy(curBalances, nb, er.Month);
                        np[er.Month - 1] += erAmount;

                        double bal = remainingBalance;
                        for (int i = 0; i < remainMonths; i++)
                        {
                            double interest = bal * monthlyRate;
                            double principal = curMonthlyPayment - interest;
                            if (principal > bal) principal = bal;
                            bal -= principal;
                            np[er.Month + i] = principal;
                            ni[er.Month + i] = interest;
                            nb[er.Month + i] = bal;
                        }

                        curPrincipals = np;
                        curInterests = ni;
                        curBalances = nb;
                        notes.Add($"第{er.Month}个月后月供调为{curMonthlyPayment:N2}元");
                    }
                }
                else
                {
                    // ============ 等额本金 ============
                    double curMonthlyPrincipal = loanAmount / totalMonths;

                    if (er.Method == 0) // 缩短年限
                    {
                        int remainMonths = (int)Math.Ceiling(remainingBalance / curMonthlyPrincipal);
                        int newTotal = er.Month + remainMonths;

                        var np = new double[newTotal];
                        var ni = new double[newTotal];
                        var nb = new double[newTotal];

                        Array.Copy(curPrincipals, np, er.Month);
                        Array.Copy(curInterests, ni, er.Month);
                        Array.Copy(curBalances, nb, er.Month);
                        np[er.Month - 1] += erAmount;

                        double bal = remainingBalance;
                        for (int i = 0; i < remainMonths; i++)
                        {
                            double principal = Math.Min(curMonthlyPrincipal, bal);
                            double interest = bal * monthlyRate;
                            bal -= principal;
                            np[er.Month + i] = principal;
                            ni[er.Month + i] = interest;
                            nb[er.Month + i] = bal;
                        }

                        curPrincipals = np;
                        curInterests = ni;
                        curBalances = nb;
                        int shortened = curMonths - newTotal;
                        curMonths = newTotal;
                        notes.Add($"第{er.Month}个月后缩短{shortened}个月");
                    }
                    else // 减少月供
                    {
                        int remainMonths = curMonths - er.Month;
                        double newMonthlyPrincipal = remainingBalance / remainMonths;

                        var np = new double[curMonths];
                        var ni = new double[curMonths];
                        var nb = new double[curMonths];

                        Array.Copy(curPrincipals, np, er.Month);
                        Array.Copy(curInterests, ni, er.Month);
                        Array.Copy(curBalances, nb, er.Month);
                        np[er.Month - 1] += erAmount;

                        double bal = remainingBalance;
                        for (int i = 0; i < remainMonths; i++)
                        {
                            double principal = Math.Min(newMonthlyPrincipal, bal);
                            double interest = bal * monthlyRate;
                            bal -= principal;
                            np[er.Month + i] = principal;
                            ni[er.Month + i] = interest;
                            nb[er.Month + i] = bal;
                        }

                        curPrincipals = np;
                        curInterests = ni;
                        curBalances = nb;
                        double firstMonthPay = newMonthlyPrincipal + remainingBalance * monthlyRate;
                        notes.Add($"第{er.Month}个月后月供调为{firstMonthPay:N2}元");
                    }
                }
            }
        }

        double totalPayment = curPrincipals.Sum() + curInterests.Sum();
        double newTotalInterest = curInterests.Sum();
        double savedInterest = origTotalInterest - newTotalInterest;
        string earlyNote = string.Join("；", notes);

        var earlyMonths = new HashSet<int>(notes.Select(n => {
            var m = earlyRepayments?.FirstOrDefault()?.Month ?? 0;
            return m;
        }));
        // Build set of all early repayment months from the data
        if (earlyRepayments != null)
        {
            earlyMonths = new HashSet<int>(earlyRepayments
                .Where(er => er.Month <= curMonths)
                .Select(er => er.Month - 1));
        }

        return new CalcResult(curPrincipals, curInterests, curBalances, curMonths,
            totalPayment, newTotalInterest, savedInterest, earlyApplied, earlyNote, isEqualPrincipal,
            loanAmount, totalMonths, monthlyRate, earlyMonths);
    }
}

public record ScheduleResult(double[] Principals, double[] Interests, double[] Balances);

public record CalcResult(
    double[] Principals, double[] Interests, double[] Balances,
    int TotalMonths, double TotalPayment, double TotalInterest, double SavedInterest,
    bool EarlyApplied, string EarlyNote, bool IsEqualPrincipal,
    double LoanAmount, int OriginalMonths, double MonthlyRate, HashSet<int> EarlyMonths)
{
    public string? Error { get; }
    public bool HasError => Error != null;

    public CalcResult(string error) : this(
        Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>(),
        0, 0, 0, 0, false, "", false, 0, 0, 0, new HashSet<int>())
    {
        Error = error;
    }

    public string MonthlyPaymentText
    {
        get
        {
            if (TotalMonths == 0) return "-";
            if (IsEqualPrincipal)
            {
                double first = Principals[0] + Interests[0];
                double last = Principals[TotalMonths - 1] + Interests[TotalMonths - 1];
                return $"首月 {first:N0} → 末月 {last:N0}";
            }
            return $"{(Principals[0] + Interests[0]):N0}";
        }
    }

    public string TotalInterestText => $"{TotalInterest:N0}  （{TotalInterest / LoanAmount * 100:F2}%）";

    public string SavedInterestText
    {
        get
        {
            if (!EarlyApplied || SavedInterest <= 0) return "—";
            double pct = SavedInterest / (TotalInterest + SavedInterest) * 100;
            return $"{SavedInterest:N0}  （-{pct:F1}%）";
        }
    }

    public bool IsEarlyMonth(int monthIndex) =>
        EarlyMonths.Contains(monthIndex);
}
