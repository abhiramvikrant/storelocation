using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;


namespace StoreLocations
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CheckFolders();
            fswStoreLocation.Path = string.Format(@"{0}\incoming", Path.GetDirectoryName(Application.ExecutablePath));

        }



        private void CheckFolders()
        {
            List<string> dirPaths = new List<string>();
            string[] allPath = { "incoming", "outgoing", "success", "errors", @"errors\failures" };
            dirPaths.AddRange(allPath);
            foreach (var dir in dirPaths)
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

        }

        private void fswStoreLocation_Created(object sender, FileSystemEventArgs e)
        {
            int r = Convert.ToInt32(lblTotalProcessed.Text);
            lblTotalProcessed.Text = Convert.ToString(r + 1);
            Thread t = new Thread(ReadTextAsync);
            t.IsBackground = true;
            t.Start(e.FullPath);

            //ReadTextAsync(e.FullPath);

        }

      
        private Object thisLock = new Object();
        private  void ReadTextAsync(object filepath)
        {
            string con = "";
            lock (thisLock)
            {
                long msec = Convert.ToInt32(lblTime.Text);
                Stopwatch sw = new Stopwatch();
                sw.Start();

                MethodInvoker inpos = delegate
                {
                    int x = Convert.ToInt32(lblProgress.Text);
                    lblProgress.Text = Convert.ToString(x + 1);
                };
                this.Invoke(inpos);
                //check for file lock
                while (IsFileLoacked(new FileInfo(filepath.ToString())))
                {
                    Thread.Sleep(1000);
                }
                con = File.ReadAllText(filepath.ToString());
                ReadJson(con.ToLower(), filepath.ToString());
                sw.Stop();
                long elap = sw.ElapsedMilliseconds;
                MethodInvoker inneg = delegate
                {
                    int x = Convert.ToInt32(lblProgress.Text);
                    if (x != 0)
                        lblProgress.Text = Convert.ToString(x - 1);
                    else
                        lblProgress.Text = "0";
                };
                this.Invoke(inneg);
                if (elap > msec)
                {
                    MethodInvoker inv = delegate
                    {
                        lblTime.Text = elap.ToString();
                    };
                    this.Invoke(inv);
                }
            }
         
               

            
        }

        //check whether the file is locked by another  process or not
        private  bool IsFileLoacked(FileInfo f)
        {
            FileStream stream = null;

            try
            {
                stream = f.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException )
            {

                return true;
            }

            finally
            {
                if (stream != null)
                    stream.Close();

            }
            return false;
        }
        public void ReadJson(string content, string filepath)
        {
            try
            {

          
            List<StoreLocationModel> models = JsonConvert.DeserializeObject<List<StoreLocationModel>>(content);
           

          
            //check for error
            foreach (StoreLocationModel item in models)
            {
                if (string.IsNullOrEmpty(item.city))
                {
                    WriteErrorFile("City value is missing", filepath);                    
                    return;
                }
                else if (string.IsNullOrEmpty(item.name))
                {
                   WriteErrorFile("Name value is missing", filepath);                    
                    return;
                }
                else if (string.IsNullOrEmpty(item.type))
                {
                    WriteErrorFile("Type value is missing", filepath);                   
                    return;
                }
            }
            // group by city and type to get the count
            var aggregate = models.GroupBy(x => new { x.city, x.type })
                 .Select(g => new StoreLocationAggregate { city = g.Key.city, type = g.Key.type, Count = g.Count() })
                .OrderBy(o =>  o.city).ThenBy(o=> o.type).ToList();
            lock(thisLock){ 
            WriteCSVFile(aggregate, filepath);
            }
            }
            catch (Exception ex)
            {

                WriteErrorFile(ex.Message, filepath);
               
            }
        }

        private void WriteCSVFile(List<StoreLocationAggregate> agg, string filepath)
        {
           
            StringBuilder csvBuilder = new StringBuilder();         
            csvBuilder.AppendLine($"\"City\",\"Type\",\"Count\"");          
            foreach (var item in agg)
            {           
                csvBuilder.AppendLine($"\"{item.city}\",\"{item.type}\",{item.Count}");               
            }
            try
            {
                string fname = string.Format(@"outgoing\{0}.temp",Path.GetFileNameWithoutExtension(filepath));
                string cname = string.Format(@"outgoing\{0}.csv", Path.GetFileNameWithoutExtension(filepath));
                lock (thisLock)
                {
                    if (File.Exists(fname))
                     {
                        File.Delete(fname);
                    }

                    if (File.Exists(cname))
                    {
                        File.Delete(cname);
                    }
                    File.WriteAllText(fname, csvBuilder.ToString());
                    File.Move(fname, cname);
                }
                Upatelable(lblSuccess);
                string sucfile = string.Format(@"{0}\{1}\{2}", Path.GetDirectoryName(Application.ExecutablePath), "success", Path.GetFileName(filepath));
                if (File.Exists(sucfile))
                {
                    File.Delete(sucfile);
                }
                File.Move(filepath,sucfile);
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.Write(ex.Message);
            }

        }
        // write to error file
        private void WriteErrorFile(string reason, string fullpath)
        {
            try
            {
                string fname = string.Format("errors/{0}.err", Path.GetFileNameWithoutExtension(fullpath));
                if (File.Exists(fname))
                {
                    File.Delete(fname);
                }
                File.WriteAllText(fname, reason);
                string errorpath = string.Format(@"{0}\{1}\{2}", Path.GetDirectoryName(Application.ExecutablePath), @"errors\failures", Path.GetFileName(fullpath));
                if (File.Exists(errorpath))
                {
                    File.Delete(errorpath);
                }
                File.Move(fullpath, errorpath);
                Upatelable(lblErrors);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.Message);
            }
        }

        private void Upatelable(Label lbl)
        {
            MethodInvoker inv = delegate
            {
                int x = Convert.ToInt32(lbl.Text);
                x += 1;
                lbl.Text = x.ToString();
            };
            this.Invoke(inv);
        }

    }
}

