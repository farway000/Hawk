﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls.WpfPropertyGrid.Attributes;
using Hawk.Core.Connectors;
using Hawk.Core.Utils;
using Hawk.Core.Utils.Logs;
using Hawk.Core.Utils.Plugins;
using Hawk.ETL.Plugins.Transformers;
using HtmlAgilityPack;

namespace Hawk.ETL.Crawlers
{
    [XFrmWork("XPath筛选器", "通过XPath选取html中的子节点文档")]
    public class XPathTF : TransformerBase
    {
        public XPathTF()
        {
            IsManyData = ScriptWorkMode.One;
        }

        [LocalizedDisplayName("XPath路径")]
        public string XPath { get; set; }

        [LocalizedDisplayName("工作模式")]
        [LocalizedDescription("当要获取符合XPath语法的多个结果时选List，只获取一条选One,其行为可参考“网页采集器”")]
        public ScriptWorkMode IsManyData { get; set; }

        [LocalizedDisplayName("获取正文")]
        [LocalizedDescription("勾选此项后，会自动提取新闻正文，XPath路径可为空")]
        public bool GetText { get; set; }

        [LocalizedDisplayName("获取正文HTML")]
        [LocalizedDescription("勾选此项后，会自动提取新闻正文的HTML，XPath路径可为空")]
        public bool GetTextHtml { get; set; }

        [LocalizedDisplayName("获取节点数量")]
        [LocalizedDescription("获取符合XPath语法的节点的数量")]
        public bool GetCount { get; set; }

        protected override IEnumerable<IFreeDocument> InternalTransformManyData(IFreeDocument data)
        {
            var item = data[Column];
            var docu = new HtmlDocument();

            docu.LoadHtml(item.ToString());
            var p2 = docu.DocumentNode.SelectNodes(XPath);
            if (p2 == null)
                return new List<IFreeDocument>();
            return p2.Select(node =>
            {
                var doc = new FreeDocument();
                doc.Add("Text", node.GetNodeText());
                doc.Add("HTML", node.InnerHtml);
                doc.Add("OHTML", node.OuterHtml);
                return doc.MergeQuery(data, NewColumn);
            });
        }

        public override bool Init(IEnumerable<IFreeDocument> docus)
        {
            IsMultiYield = IsManyData==ScriptWorkMode.List;
            return base.Init(docus);
        }

        public override object TransformData(IFreeDocument document)
        {
            var item = document[Column];

            if (item is IFreeDocument)
            {
                return (item as IFreeDocument).GetDataFromXPath(XPath);
            }
            var docu = new HtmlDocument();

            docu.LoadHtml(item.ToString());
            if (GetText)
            {
                var path = docu.DocumentNode.GetTextNode();
                var textnode = docu.DocumentNode.SelectSingleNodePlus(path,SelectorFormat.XPath);
                if (textnode != null)
                    return textnode.GetNodeText();
            }
            if (GetTextHtml)
            {
                var path = docu.DocumentNode.GetTextNode();
                var textnode = docu.DocumentNode.SelectSingleNodePlus(path,SelectorFormat.XPath);
                if (textnode != null)
                    return textnode.InnerHtml;
            }
            if (GetCount)
            {
                var textnode = docu.DocumentNode.SelectNodes(XPath);
                return textnode.Count;
            }

            var res = docu.DocumentNode.GetDataFromXPath(document.Query(XPath));
            if (res == null)
            {
            }
            return res;
        }
    }


    [XFrmWork("门类枚举", "要拖入HTML文本列,可将页面中的门类，用Cross模式组合起来，适合于爬虫无法抓取全部页面，但可以按分类抓取的情况。需调用网页采集器，具体参考文档-门类枚举")]
    public class XPathTF2 : ResponseTF
    {
        private Dictionary<string, string> xpaths;

        [Browsable(false)]
        public override string HeaderFilter { get; set; }

        public override bool Init(IEnumerable<IFreeDocument> docus)
        {
            base.Init(docus);
            if (crawler == null)
                return false;
            IsMultiYield = true;
            xpaths = crawler.CrawlItems.GroupBy(d => d.Name).Select(d =>
            {
                var column = d.Key;
                var path = XPath.GetMaxCompareXPath(d.Select(d2 => d2.XPath).ToList());
                return new {Column = column, XPath = path};
            }).ToDictionary(d => d.Column, d => d.XPath);
            return true;
        }

        private IEnumerable<IFreeDocument> Get(HtmlDocument docu, IEnumerable<IFreeDocument> source, string name,
            string xpath)
        {
            HtmlNodeCollection nodes;
            try
            {
                nodes = docu.DocumentNode.SelectNodes(xpath);
            }
            catch (Exception ex)
            {
                XLogSys.Print.Warn("XPath表达式错误: " + xpath);
                return source;
            }
            if (nodes.Count == 0)
            {
                XLogSys.Print.Warn("XPath表达式: " + xpath + "获取的节点数量为0");
                return source;
            }
            var new_docs = nodes.Select(node =>
            {
                var doc = new FreeDocument();
                doc.Add(name + "_text", node.GetNodeText());
                doc.Add(name + "_ohtml", node.OuterHtml);
                return doc;
            });
            return new_docs.Cross(source);
        }

        protected override IEnumerable<IFreeDocument> InternalTransformManyData(IFreeDocument data)
        {
            {
                var item = data[Column];
                var docu = new HtmlDocument();

                docu.LoadHtml(item.ToString());
                var d = new FreeDocument();
                d.MergeQuery(data, NewColumn);
                IEnumerable<IFreeDocument> source = new List<IFreeDocument> {d};
                source = xpaths.Aggregate(source, (current, xpath) => Get(docu, current, xpath.Key, xpath.Value));
                return source.ToList();
            }
        }
    }
}