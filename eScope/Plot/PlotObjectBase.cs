using eScope.Command;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ScottPlot;
using ScottPlot.Hatches;
using ScottPlot.Plottables;
using System.Text.Json;
using static eScope.Plot.eScopePlot;
using static IronPython.Modules._ast;
namespace eScope.Plot
{
    public interface IPlotContext
    {
        int DarkMode { get; }
        bool Exists(string name);
        List<ChannelCtrl> GetSignalList();
        ChannelCtrl? GetSignalFromId(Guid? id);
        ChannelCtrl? GetSignalFromName(string? name);
        void UpdateMarkers();
        IList<CtrlBase> Ctrls { get; }
        eScopePlot Plot { get; }
        eScope Scope { get; }
        Dictionary<string, List<double>> ScriptGlobalsDict { get; }
        int GetNextSignalUid();
    }
    public abstract class DataBase
    {
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF0000";
    }
    public abstract class CtrlBase
    {
        public DataBase DataBase;
        public double XDisplayEnd { get; set; }
        public int XLastIdxCompute { get; set; }
        public double XTimeEnd { get; set; }
        public string Legend { get; set; } = string.Empty;
        public ToolStripItem Item { get; } = new System.Windows.Forms.ToolStripMenuItem();
        protected IPlotContext Context { get; set; }
        abstract public void Add();
        abstract public void Remove();
        abstract public void Edit();
        abstract public void Configure();
        public abstract bool IsVisible();

        public abstract void SetVisibility(bool isVisible);
        public virtual void Update() { }
        public virtual void YZoomIn(Pixel mousePixel, bool centerZero = false) {}
        public virtual void YZoomOut(Pixel mousePixel, bool centerZero = false) {}
        public virtual void ZoomIn() { }
        public virtual void ZoomOut() { }
        public virtual void HighLight() { }
        public virtual void LowLight() { }
        public abstract double GetNearestDistance(Pixel p, float maxDistance);
        public virtual void Move(Pixel pix_to, Pixel pix_from, moveDirection dir) { }
        public virtual void UpdateLegend(double x) { }
        protected CtrlBase(DataBase data, IPlotContext ctx)
        {
            DataBase = data;
            Context = ctx;
        }

        // settings of the object (simple values, no data buffer), saved / restored by the undo of an edit
        public Dictionary<string, object?> CaptureSettings()
            => SettingProperties().ToDictionary(p => p.Name, p => p.GetValue(DataBase));
        public void ApplySettings(Dictionary<string, object?> settings)
        {
            foreach (var p in SettingProperties())
            {
                if (settings.TryGetValue(p.Name, out var v))
                    p.SetValue(DataBase, v);
            }
            Configure();
            Context.Plot.RefreshReq = true;
        }
        IEnumerable<System.Reflection.PropertyInfo> SettingProperties()
            => DataBase.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                            && (p.PropertyType.IsValueType || p.PropertyType == typeof(string))
                            && IsSetting(p.Name));
        // values not set by the editor (identity, position of the data...)
        protected virtual bool IsSetting(string property) => true;

        // editor of the object: the change is applied through an undoable command
        protected void RunEditor(Form dialog)
        {
            var before = CaptureSettings();
            if (dialog.ShowDialog() != DialogResult.OK)
                return;
            var cmd = new CmdEditObj(this, before);
            if (cmd.IsEmpty)
                Configure(); // nothing to undo
            else
                commandMgr.ExecuteCommand(cmd);
        }
    }
    public abstract class CtrlBase<TData> : CtrlBase
    where TData : DataBase
    {
        public TData Data => (TData)DataBase;
        protected CtrlBase(TData data, IPlotContext ctx)
            : base(data, ctx)
        {
        }
    }
    public class dataHolder // use to dynamically share data between several instance
    {
        public event EventHandler? Changed; // raised whenever Data is written, so linked instances can refresh their rendering
        private double _data = 0;
        public double Data
        {
            get => _data;
            set
            {
                _data = value;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    public class EventArgsOnModified : EventArgs // use to notify when source change it own data reference
    {
        public linkHolder LhSrc; // new data source

        public EventArgsOnModified(linkHolder lhSrc)
        {
            LhSrc = lhSrc;
        }
    }
    // data link of type 1 to many
    public class linkHolder
    {
        public Guid Id { get; set; } // current instance ID
        public Guid? SrcId { get; set; } // instance ID of source used at application init to restore reference from json deserialization
        [JsonIgnore]
        public linkHolder? LhSrc;
        [JsonIgnore]
        public  List<linkHolder> DstLhList = new List<linkHolder>(); // list of destination instance using our data (avoid circular link)
        [JsonIgnore]
        public dataHolder _DataRef = new(); // source data that can be shared with 
        //[JsonIgnore]
        public event EventHandler<EventArgsOnModified>? Modified; //  call when source data is modified to give all listener the new src data
        public event EventHandler? DataChanged; // raised whenever the current DataRef's value changes, or DataRef itself is swapped (link/unlink) - survives relinking, unlike subscribing to DataRef.Changed directly
        [JsonIgnore]
        public dataHolder DataRef // intermediary data to add notification of all destination register
        {
            get => _DataRef;
            set
            {
                if (_DataRef != null)
                {
                    _DataRef.Changed -= OnDataRefValueChanged;
                }
                _DataRef = value;
                if (_DataRef != null)
                {
                    _DataRef.Changed += OnDataRefValueChanged;
                }
                Modified?.Invoke(this, new EventArgsOnModified(this));
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private void OnDataRefValueChanged(object? sender, EventArgs e)
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
        public linkHolder(Guid id, Guid? srcId)
        {
            Id = id;
            SrcId = srcId;
            LhSrc = null;
            _DataRef.Changed += OnDataRefValueChanged;
        }
        public void OnSourceModified(object? sender, EventArgsOnModified e)
        {
            DataRef = e.LhSrc.DataRef; // will forward to any register destination
            SrcId = e.LhSrc.Id;
            LhSrc = e.LhSrc; 
        }
        public void Unlink()
        {
            DataRef = new dataHolder { Data = _DataRef.Data }; // copy current data value into it own data ref
            SrcId = null;

            if(LhSrc != null)
            {
                LhSrc.Modified -= OnSourceModified;
                LhSrc.DstLhList.Remove(this);
                LhSrc = null;
            }
        }
        public void Link(linkHolder lhSrc)
        {
            if (Id == lhSrc.Id) return;
            DataRef = lhSrc.DataRef;
            SrcId = lhSrc.Id;
            lhSrc.DstLhList.Add(this);
            lhSrc.Modified += OnSourceModified;
            LhSrc = lhSrc;
        }
        public List<Guid> GetLinkIdList() // Get all guid id link to the current data
        {
            List<Guid> idList = new();
            foreach(var lh in DstLhList)
            {
                idList.Add(lh.Id);
                idList.AddRange(lh.GetLinkIdList());
            }
            return idList;
        }
    }
}
