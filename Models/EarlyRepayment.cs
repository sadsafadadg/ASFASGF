namespace MortgageCalc.Models;

public class EarlyRepayment
{
    public int Month { get; set; }       // 在第几个月
    public double Amount { get; set; }   // 提前还金额 (Method=2时忽略)
    public int Method { get; set; }      // 0=缩短年限 1=减少月供 2=一次性还清
}
