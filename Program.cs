using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace QuincyDataKeeper
{
    internal class Program
    {
        const string STATUS_ONLINE = "online";
        const string STATUS_OFFLINE = "offline";
        const string ACTION_BEGIN = "begin";
        const string ACTION_RENAME = "rename";
        const string ACTION_END = "end";

        static readonly JavaScriptSerializer serializer = new JavaScriptSerializer();

        static void Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:23466/");
            listener.Start();
            Console.WriteLine("正在监听http://localhost:23466......");
            var timedic = new Dictionary<string, LiveData>();
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                if (request.HttpMethod == "GET" && request.RawUrl.StartsWith("/livedata"))
                {
                    string rawQuery = request.Url.Query;
                    var query = HttpUtility.ParseQueryString(rawQuery, Encoding.UTF8); //自行转换原始请求链接，防止中文乱码
                    string id = query["id"];
                    string status = query["status"];
                    string title = query["title"];
                    DateTime time = DateTime.Now;
                    bool on = timedic.ContainsKey(id); //如果数据字典里已经有该主播的数据，则on为true
                    Console.WriteLine($"[消息 {time}]主播{id}现在状态为{status}，标题为{title}。更改标题：{on && status == STATUS_ONLINE}");

                    if (status == STATUS_ONLINE && !on)
                    {//主播首次开播 
                        timedic.Add(id, new LiveData(title, time));
                        var respData = ConstructResponse(ACTION_BEGIN, "", "");
                        SendResponse(respData, response);
                    }
                    else if (status == STATUS_ONLINE && on)
                    {//主播更改标题
                        if (timedic.ContainsKey(id))
                        {
                            string prevTitle = timedic[id].Title; //更改标题时要返回前序标题
                            timedic[id].Title = title;
                            var respData = ConstructResponse(ACTION_RENAME, prevTitle, "");
                            SendResponse(respData, response);
                        }
                        else
                        {
                            var respData = ConstructResponse(ACTION_RENAME, "[错误]获取前序标题失败", "");
                            SendResponse(respData, response);
                        }
                    }
                    else if (status == STATUS_OFFLINE)
                    {//主播下播
                        if (timedic.ContainsKey(id))
                        {
                            DateTime begin = timedic[id].Begin;
                            string timeSpan = DisplayTimeGap(begin, time);
                            timedic.Remove(id); //下播后删除该主播的数据
                            var respData = ConstructResponse(ACTION_END, title, timeSpan);
                            SendResponse(respData, response);
                        }
                        else
                        {
                            var respData = ConstructResponse(ACTION_END, "", "[错误]获取开播时长失败");
                            SendResponse(respData, response);
                        }
                    }
                }
            }
        }
        static string DisplayTimeGap(DateTime begin, DateTime end) //用于计算开播时间
        {
            TimeSpan span = end.Subtract(begin);
            int totalMinutes = (int)span.TotalMinutes;
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            if (hours == 0)
                return $"{minutes}分钟";
            return $"{hours}小时{minutes}分钟";
        }
        static Dictionary<string, string> ConstructResponse(string action, string prevTitle, string timeSpan)
        {
            return new Dictionary<string, string>
            {
                {"action",action },//action的值为begin, rename或end
                {"prevTitle",prevTitle},
                {"timeSpan",timeSpan}
            };
        }
        static void SendResponse(Dictionary<string, string> data, HttpListenerResponse response)
        {
            string resp = serializer.Serialize(data); //把Dictionary序列化为字符串
            byte[] json = Encoding.UTF8.GetBytes(resp);
            response.ContentType = "application/json"; //注意要以json格式发送
            response.ContentLength64 = json.Length;
            response.OutputStream.Write(json, 0, json.Length);
            response.OutputStream.Close();
        }
    }
    class LiveData
    {
        public string Title { get; set; }
        public DateTime Begin { get; }
        public LiveData(string title, DateTime begin)
        {
            Title = title;
            Begin = begin;
        }
    }
}
