using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using log4net;
using System.Xml;
using ICReport;
using System.ComponentModel;
using util;

namespace Locomotive
{
    class Program
    {
        public static ILog log = LogManager.GetLogger(string.Empty);
        private static string DOCUMENT_NAME = "Сводка по времени работы локомотива (1.15)"; // !!!

        static void Main(string[] args)
        {
            // Для теста
            //args = new string[] { "host:10.90.101.1;keyspace:as_pkpf; reqtype:FORM_PARAMETERS" };
            //args = new string[] { "host:10.90.101.2;keyspace:as_pkpf; dtstart:01.11.2020; dtend:30.12.2020;" };
            // Инициализация отчета
            Report report = new Report(DOCUMENT_NAME);
            if (report.Init(args, new string[] { }) == false) return;

            if (report.getParameters().ContainsKey("reqtype") && report.getParameters()["reqtype"] == "FORM_PARAMETERS")
            {
                new FormParameters().Execute(report);
                return;
            }
            else
            {
                // Проверка на корректность параметров или вывод наименования, если параметров нет
                if (!(report.getParameters().ContainsKey("dtstart") &&
                    report.getParameters().ContainsKey("dtend")))
                {
                    Console.Write("ERROR: Недостаточно входных параметров для построения отчета");
                    report.shutdown();
                    return;
                }
            }


            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            // Создание отчета
            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            XmlDocument doc = new XmlDocument();
            XmlElement root = (XmlElement)doc.AppendChild(doc.CreateElement("Data"));

            // Создаем DataSet main_info
            {
                // Описание полей ------------------------------------------------
                Dictionary<string, string> fields = new Dictionary<string, string>()
                {
                    { "dtstart","string" },
                    { "dtend","string"}
                };
                XmlElement DS = report.CreateDataSet(doc, "main_info", fields);
                root.AppendChild(DS);

                XmlElement Rows = doc.CreateElement("Rows");
                DS.AppendChild(Rows);

                // Обработка данных -----------------------------------------------
                try
                {
                    XmlElement xrow = doc.CreateElement("row");
                    xrow.AppendChild(doc.CreateElement("dtstart")).InnerText = (report.getParameters())["dtstart"];
                    xrow.AppendChild(doc.CreateElement("dtend")).InnerText = (report.getParameters())["dtend"];
                    Rows.AppendChild(xrow);
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                }
            }

            // Создаем DataSet autotransport_list
            {
                // Описание полей ------------------------------------------------
                Dictionary<string, string> fields = new Dictionary<string, string>()
                {
                    { "num","int" },
                    { "date_start","string"},
                    { "teplovoz_num","string"},
                    { "call_sign","string"},
                    { "brigade","int"},
                    { "fio_dispetcher","string"},
                    { "time_start","string"},
                    { "date_end","string"},
                    { "work_time","string"},
                    { "lokomotiv_sost_brigade","string"},
                    { "comment","string"},
                    { "work_time_extension","string"},
                    { "is_weekend","int"}
                };
                XmlElement DS = report.CreateDataSet(doc, "lokomotiv_list", fields);
                root.AppendChild(DS);

                XmlElement Rows = doc.CreateElement("Rows");
                DS.AppendChild(Rows);

                // Обработка данных -----------------------------------------------
                try
                {
                    DateTime dstart = DateTime.ParseExact((report.getParameters())["dtstart"], "dd.MM.yyyy", null);
                    DateTime dend = DateTime.ParseExact((report.getParameters())["dtend"], "dd.MM.yyyy", null);
                    DateTime dend_calc = dend.AddDays(1);
                    var session = report.getSession();
                   
                    var ps = session.Prepare("SELECT * FROM tb_railway_lokomotiv "+
                                             "where year in (?,?) and date_start >= ? and date_start < ?");

                    var rs = session.Execute(ps.Bind(dstart.Year, dend.Year, dstart, dend_calc).SetConsistencyLevel(Cassandra.ConsistencyLevel.LocalQuorum));

                    List<TItem> items = new List<TItem>();
                    foreach (var row in rs)
                    {                       
                        TItem item = new TItem();
                        item.date_start = row.GetValue<DateTime>("date_start");
                        item.teplovoz_num = row.GetValue<int>("teplovoz_num");
                        item.row = row;
                        items.Add(item);
                    }
                    items.Sort((a, b) => {
                        int r = a.date_start.CompareTo(b.date_start);
                        if (0 != r) { return r; }
                        return a.teplovoz_num.CompareTo(b.teplovoz_num);
                    });

                    int npp = 0;
                    int? ival;
                    DateTime? date_end;
                    DayOfWeek day_week;
                    foreach (var item in items)
                    {
                        npp++;
          

                     XmlElement xrow = doc.CreateElement("row");

                     xrow.AppendChild(doc.CreateElement("num")).InnerText = String.Format("{0}",npp);

                     date_end = item.row.GetValue<DateTime?>("date_end");
                     ival = item.row.GetValue<int?>("call_sign");
                     xrow.AppendChild(doc.CreateElement("date_start")).InnerText = item.date_start.ToString("dd.MM.yyyy");
                     xrow.AppendChild(doc.CreateElement("teplovoz_num")).InnerText = item.teplovoz_num.ToString();
                     xrow.AppendChild(doc.CreateElement("call_sign")).InnerText = (ival.HasValue)? ival.ToString():null;
                     xrow.AppendChild(doc.CreateElement("brigade")).InnerText = String.Format("{0}", item.row.GetValue<int>("brigade"));
                     xrow.AppendChild(doc.CreateElement("fio_dispetcher")).InnerText = item.row.GetValue<string>("fio_dispetcher");
                     xrow.AppendChild(doc.CreateElement("time_start")).InnerText = item.date_start.ToString("HH:mm"); 
                     xrow.AppendChild(doc.CreateElement("date_end")).InnerText = ((date_end.HasValue)? date_end.Value.ToString("HH:mm"):"");

                     ival = item.row.GetValue<int?>("time_work");
                     if (ival.HasValue)
                     {
                       xrow.AppendChild(doc.CreateElement("work_time")).InnerText = String.Format("{0:D2}:{1:D2}", (int)ival.Value / 60, ival.Value % 60);
                     }


                     // Обработка бригады 
                     String sostav_brig = "";
                     IEnumerable<IDictionary<String,String>> brig = item.row.GetValue<IEnumerable<IDictionary<String,String>>>("lokomotiv_sost_brigade")??new List<IDictionary<String, String>>();
                     foreach (IDictionary<String, String> itemB in brig)
                     {
                            if (itemB.ContainsKey("brigade_pos") && itemB.ContainsKey("brigade_fio"))
                            {
                                sostav_brig += itemB["brigade_pos"] + " " + itemB["brigade_fio"] + ";\n";
                            }
                     }
                     xrow.AppendChild(doc.CreateElement("lokomotiv_sost_brigade")).InnerText = sostav_brig;


                     xrow.AppendChild(doc.CreateElement("comment")).InnerText = item.row.GetValue<string>("comment");
                    /*
                     // Обработка часов после 20:00
                     DateTime date_end_20 = date_end.Date.Add(new TimeSpan(20, 0, 0));
                     if (date_end_20 < date_end)
                     {
                        var residual20 = (date_end - date_end.Date.Add(new TimeSpan(20, 0, 0))).Duration();
                        xrow.AppendChild(doc.CreateElement("work_time_extension")).InnerText = String.Format("{0:D2}:{1:D2}", residual20.Hours, residual20.Minutes);
                     } else  {
                            xrow.AppendChild(doc.CreateElement("work_time_extension")).InnerText = "";
                     }
                   */
                     // Обработка дня недели
                     string is_weekend = "0";
                     day_week = item.date_start.DayOfWeek;
                     if (day_week == DayOfWeek.Saturday || day_week == DayOfWeek.Sunday) is_weekend = "1";
                     xrow.AppendChild(doc.CreateElement("is_weekend")).InnerText = is_weekend;
                     
                     Rows.AppendChild(xrow);

                  }
                }
                catch (Exception ex)
                {
                     log.Error(ex.Message);
                    Console.Write("ERROR: "+ex.Message);
                    report.shutdown();
                    return;
                }
            }

            report.shutdown();  // Закрытие соединения

            Console.Write(doc.OuterXml);

        }
    }

}