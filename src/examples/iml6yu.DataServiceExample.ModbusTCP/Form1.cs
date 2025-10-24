using iml6yu.DataService.Modbus.Configs;
using iml6yu.DataService.ModbusTCP; 
using System.Configuration;
using System.Data;
using System.Windows.Forms;

namespace iml6yu.DataServiceExample.ModbusTCP
{
    public partial class Form1 : Form
    {
        DataServiceModbusTCP dataService;
        System.Threading.CancellationTokenSource CancellationTokenSource;
        DataTable dataTable;
        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            var optionFile = Path.Combine(AppContext.BaseDirectory, ConfigurationManager.AppSettings.Get("option"));
            if (!File.Exists(optionFile))
            {
                MessageBox.Show($"文件{optionFile}不存在！点击确定推出程序！", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
            DataServiceModbusOption? option =
              Newtonsoft.Json.JsonConvert.DeserializeObject<DataServiceModbusOption>(File.ReadAllText(optionFile));

            dataService =
                 new DataServiceModbusTCP(option);

            DrawDataGridView(option.Slaves);
        }

        private Task StartGetDatas()
        {
            return Task.Run(async () =>
            {
                while (!CancellationTokenSource.IsCancellationRequested)
                {
                    //var datas = dataService.GetDatas();
                   
                    //foreach (var slave in datas.Keys)
                    //{
                    //    int rowsCount = 0;
                    //    foreach (var store in datas[slave].Keys)
                    //    {
                    //        var index = 0;
                    //        foreach (var item in datas[slave][store])
                    //        {
                    //            DataRow dr;
                    //            if (dataTable.Rows.Count < index + rowsCount + 1)
                    //                dr = dataTable.NewRow();
                    //            else
                    //                dr = dataTable.Rows[index + rowsCount];
                    //            dr[$"slave_{slave.Id}_address"] = $"{slave.Id}.{store.StoreType.ToString()}.{store.StartAddress + index}";
                    //            dr[$"slave_{slave.Id}_value"] = item.ToString();
                    //            if (dataTable.Rows.Count < index + rowsCount + 1)
                    //                dataTable.Rows.Add(dr);
                    //            index++;
                    //        }
                    //        rowsCount += index;
                    //    } 
                    //}
                    //if (dataGridView1.InvokeRequired)
                    //{
                    //    // 异步切换到UI线程
                    //    dataGridView1.BeginInvoke(new Action(() =>
                    //    {
                    //        //bindingSource1.DataSource = dataTable; // 更新BindingSource
                    //        dataGridView1.Refresh(); // 可选：强制刷新界面 
                    //    }));
                    //}
                    //else
                    //{
                    //    bindingSource1.DataSource = dataTable; // 更新BindingSource
                    //    //dataGridView1.DataSource = dataTable;
                    //}

                    await Task.Delay(1000);
                }
            });
        }

        private void DrawDataGridView(List<DataServiceModbusSlaveOption> slaves)
        {
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.DataError += DataGridView1_DataError;
            dataTable = new DataTable();
            foreach (var slave in slaves)
            {
                dataTable.Columns.Add($"slave_{slave.Id}_address");
                dataTable.Columns.Add($"slave_{slave.Id}_value");
            }
            dataGridView1.DataSource = dataTable;
        }

        private void DataGridView1_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {

        }

        /// <summary>
        /// 开启服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (dataService == null) return;
            CancellationTokenSource = new CancellationTokenSource();
            dataService.StartServicer(CancellationTokenSource.Token);
            label1.BackColor = Color.Green;
            StartGetDatas();
        }

        /// <summary>
        /// 关闭服务
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (dataService == null) return;
            if (dataService.IsRuning)
            {
                CancellationTokenSource?.Cancel();
                dataService.StopServicer();
                CancellationTokenSource?.Dispose();
                label1.BackColor = Color.Red;
            }
        }
    }
}
