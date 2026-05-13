#nullable enable

using NUnit.Framework;
using Rhizomode.Graph.Mutation;
using Rhizomode.Graph.Snapshot;
using Rhizomode.SharedKernel;

namespace Rhizomode.Graph.Tests
{
    public class GraphCommandRecordTests
    {
        [Test]
        public void AllCommands_HaveOriginField()
        {
            IGraphCommand add = new AddNodeCommand(CommandOrigin.Test, "n1", "ConstFloat", RzVector3.Zero);
            IGraphCommand remove = new RemoveNodeCommand(CommandOrigin.Test, "n1");
            IGraphCommand connect = new ConnectPortsCommand(
                CommandOrigin.Test, "e1", "n1", "Out", "n2", "In");
            IGraphCommand disconnect = new DisconnectEdgeCommand(CommandOrigin.Test, "e1");
            IGraphCommand move = new MoveNodeCommand(CommandOrigin.Test, "n1", new RzVector3(1, 2, 3));
            IGraphCommand setParam = new SetNodeParamCommand(
                CommandOrigin.Test, "n1", "Value", ParamValue.Float(0.5f));
            IGraphCommand load = new LoadGraphCommand(CommandOrigin.Test, GraphSnapshot.Empty);

            Assert.AreEqual(CommandOrigin.Test, add.Origin);
            Assert.AreEqual(CommandOrigin.Test, remove.Origin);
            Assert.AreEqual(CommandOrigin.Test, connect.Origin);
            Assert.AreEqual(CommandOrigin.Test, disconnect.Origin);
            Assert.AreEqual(CommandOrigin.Test, move.Origin);
            Assert.AreEqual(CommandOrigin.Test, setParam.Origin);
            Assert.AreEqual(CommandOrigin.Test, load.Origin);
        }

        [Test]
        public void CommandAuditLog_DetectsMultiOriginCommands()
        {
            var log = new CommandAuditLog();
            log.Record(new AddNodeCommand(CommandOrigin.Ui, "n1", "ConstFloat", RzVector3.Zero));
            log.Record(new AddNodeCommand(CommandOrigin.Interaction, "n2", "ConstFloat", RzVector3.Zero));

            CollectionAssert.Contains(log.DetectMultiOriginCommands(), nameof(AddNodeCommand));
        }
    }
}
