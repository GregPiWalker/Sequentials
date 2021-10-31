using System;
using System.Collections.Generic;
using System.Text;

namespace Sequentials.Builders
{
    public class BuildTimeNodeContext
    {
        public ActionNode PreviousSupplier { get; set; }

        public ActionNode Supplier { get; set; }

        public ActionNode Consumer { get; set; }
    }
}
