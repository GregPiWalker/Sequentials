using CleanMachine;
using CleanMachine.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sequentials.Builders
{
    public class NodeBinder
    {
        public NodeBinder(ActionNode node)
        {
            Node = node;
        }

        public List<string> ReflexKeys { get; } = new List<string>();

        public ActionNode Node { get; set; }

        //public string PreferredName { get; set; }
    }
}
