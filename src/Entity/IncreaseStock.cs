using System;
using System.Linq;
using System.Collections.Generic;
using FDDC;
using static HTMLTable;

public class IncreaseStock
{
    public struct struIncreaseStock
    {
        //公告id
        public string id;

        //增发对象
        public string PublishTarget;


        //发行方式(定价/竞价)
        public string PublishMethod;

        //增发数量
        public string IncreaseNumber;

        //增发金额
        public string IncreaseMoney;

        //锁定期（一般原则：定价36个月，竞价12个月）
        //但是这里牵涉到不同对象不同锁定期的可能性
        public string FreezeYear;

        //认购方式（现金股票）
        public string BuyMethod;
        public string GetKey()
        {
            return id + ":" + PublishTarget;
        }
    }

    internal static struIncreaseStock ConvertFromString(string str)
    {
        var Array = str.Split("\t");
        var c = new struIncreaseStock();
        c.id = Array[0];
        c.PublishTarget = Array[1];
        if (Array.Length > 2)
        {
            c.PublishMethod = Array[2];
        }
        if (Array.Length > 3)
        {
            c.IncreaseNumber = Array[3];
        }
        if (Array.Length > 4)
        {
            c.IncreaseMoney = Array[4];
        }
        if (Array.Length > 5)
        {
            c.FreezeYear = Array[5];
        }
        if (Array.Length == 7)
        {
            c.BuyMethod = Array[6];
        }
        return c;
    }


    internal static string ConvertToString(struIncreaseStock increaseStock)
    {
        var record = increaseStock.id + "," +
        increaseStock.PublishTarget + "," +
        increaseStock.PublishMethod + ",";
        record += Normalizer.NormalizeNumberResult(increaseStock.IncreaseNumber) + ",";
        record += Normalizer.NormalizeNumberResult(increaseStock.IncreaseMoney) + ",";
        if (!String.IsNullOrEmpty(increaseStock.FreezeYear) && increaseStock.FreezeYear.EndsWith("个月"))
        {
            increaseStock.FreezeYear = increaseStock.FreezeYear.Replace("个月", "");
        }
        record += increaseStock.FreezeYear + "," +
        increaseStock.BuyMethod;
        return record;
    }

    public static List<struIncreaseStock> Extract(string htmlFileName)
    {
        var fi = new System.IO.FileInfo(htmlFileName);
        Program.Logger.WriteLine("Start FileName:[" + fi.Name + "]");
        var node = HTMLEngine.Anlayze(htmlFileName);
        //公告ID
        var id = fi.Name.Replace(".html", "");
        Program.Logger.WriteLine("公告ID:" + id);

        //发行方式
        var publishMethod = getPublishMethod(node);
        //认购方式
        var buyMethod = getBuyMethod(node);
        //样本
        var increaseStock = new struIncreaseStock();
        increaseStock.id = id;
        increaseStock.PublishMethod = publishMethod;
        increaseStock.BuyMethod = buyMethod;
        var list = GetMultiTarget(node, increaseStock);
        return list;
    }


    static List<struIncreaseStock> GetMultiTarget(HTMLEngine.MyRootHtmlNode root, struIncreaseStock SampleincreaseStock)
    {
        var BuyerRule = new TableSearchRule();
        BuyerRule.Name = "认购对象";
        BuyerRule.Rule = new string[] { "认购对象", "发行对象", "发行对象名称" }.ToList();
        BuyerRule.IsEq = true;

        var BuyNumber = new TableSearchRule();
        BuyNumber.Name = "增发数量";
        BuyNumber.Rule = new string[] { "配售股数", "认购数量", "认购股份数" }.ToList();
        BuyNumber.IsEq = false;             //包含即可
        BuyNumber.Normalize = Normalizer.NormalizerStockNumber;

        var BuyMoney = new TableSearchRule();
        BuyMoney.Name = "增发金额";
        BuyMoney.Rule = new string[] { "配售金额", "认购金额" }.ToList();
        BuyMoney.IsEq = false;             //包含即可
        BuyMoney.Normalize = Normalizer.NormalizerMoney;

        var FreezeYear = new TableSearchRule();
        FreezeYear.Name = "锁定期";
        FreezeYear.Rule = new string[] { "锁定期", "限售期" }.ToList();
        FreezeYear.IsEq = false;             //包含即可
        FreezeYear.Normalize = NormalizerFreezeYear;

        var Rules = new List<TableSearchRule>();
        Rules.Add(BuyerRule);
        Rules.Add(BuyNumber);
        Rules.Add(BuyMoney);
        Rules.Add(FreezeYear);

        var result = HTMLTable.GetMultiInfo(root, Rules, true);
        var increaseStocklist = new List<struIncreaseStock>();
        foreach (var item in result)
        {
            var increase = new struIncreaseStock();
            increase.id = SampleincreaseStock.id;
            increase.BuyMethod = SampleincreaseStock.BuyMethod;
            increase.PublishMethod = SampleincreaseStock.PublishMethod;
            increase.PublishTarget = item[0].RawData;
            increase.IncreaseNumber = item[1].RawData;
            increase.IncreaseMoney = item[2].RawData;
            increase.FreezeYear = item[3].RawData;
            increaseStocklist.Add(increase);
        }
        return increaseStocklist;
    }



    static string NormalizerFreezeYear(string orgString, string TitleWord)
    {
        orgString = orgString.Replace(" ", "");
        if (orgString.Equals("十二")) return "12";

        var x1 = Utility.GetStringAfter(orgString, "日起");
        int x2;
        if (int.TryParse(x1, out x2)) return x2.ToString();
        x1 = Utility.GetStringBefore(orgString, "个月");
        if (int.TryParse(x1, out x2)) return x2.ToString();

        x1 = RegularTool.GetValueBetweenString(orgString, "日起", "个月");
        if (x1.Equals("十二")) return "12";
        if (int.TryParse(x1, out x2)) return x2.ToString();

        if (orgString.Equals("十二")) return "12";
        if (orgString.Equals("十二个月")) return "12";
        if (orgString.Equals("1年")) return "12";
        if (orgString.Equals("3年")) return "36";
        return orgString.Trim();
    }

    static string getPublishMethod(HTMLEngine.MyRootHtmlNode root)
    {
        //是否包含关键字 "询价发行"
        var Extractor = new ExtractProperty();
        var cnt = Extractor.FindWordCnt("询价发行", root);
        Program.Logger.WriteLine("询价发行(文本):" + cnt);
        if (cnt > 0) return "竞价";

        cnt = Extractor.FindWordCnt("发行价格不低于", root);
        if (cnt > 0) return "竞价";

        cnt = Extractor.FindWordCnt("发行价格为", root);
        if (cnt > 0) return "定价";

        return "";
    }

    static string getBuyMethod(HTMLEngine.MyRootHtmlNode root)
    {
        //是否包含关键字 "现金认购"
        var Extractor = new ExtractProperty();
        var cnt = Extractor.FindWordCnt("现金认购", root);
        Program.Logger.WriteLine("现金认购(文本):" + cnt);
        if (cnt > 0) return "现金";
        return "";
    }

}