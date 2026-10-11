using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eScope.Plot
{
    public sealed class PlotObjectFactory
    {
        private readonly Dictionary<Type, Func<object, IPlotContext, CtrlBase>> _map
            = new();

        public void Register<TData, TCtrl>()
            where TCtrl : CtrlBase
        {
            _map[typeof(TData)] = (data, ctx) =>
                (CtrlBase)Activator.CreateInstance(
                    typeof(TCtrl),
                    data,
                    ctx
                )!;
        }

        public CtrlBase Create(object data, IPlotContext ctx)
        {
            var dataType = data.GetType();

            if (!_map.TryGetValue(dataType, out var creator))
                throw new InvalidOperationException(
                    $"No plot object registered for {dataType.Name}");

            return creator(data, ctx);
        }
    }
}
