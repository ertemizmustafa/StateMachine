using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {



        static void Main(string[] args)
        {

            var workFlowBuilder = new WorkFlowBuilder<string>("Akış test process");

            workFlowBuilder.CreateStep(new Step { IsInitial = true, Name = "Created", DenyOnCondition = () =>  1 < 0 }, directions: new List<Direction> { new Direction { Command = "Send", ToStep = "Approval" } });

            workFlowBuilder.CreateStep(new Step { Name = "Approval" }, directions: new List<Direction> { new Direction { Command = "Approve", ToStep = "Evaluation" }, new Direction { Command = "Reject", ToStep = "Revision" } })
                    .AddBlockOfSubStep(new BlockStep { Name = "OnManager" })
                    .AddBlockOfSubStep(new BlockStep { Name = "OnDirector", SkipOnCondition = () => GiveMeSuggestion() })
                    .AddBlockOfSubStep(new BlockStep { Name = "OnGmy", SkipOnCondition = () => Calculate(3) });

            //Substeplerde
            workFlowBuilder.CreateStep(new Step { Name = "Evaluation" })
                    .AddSubStep(new SubStep { Name = "Evaluation", DenyOnCondition = () => GiveMeSuggestion() }, directions: new List<Direction> { new Direction { Command = "Send", ToStep = "OwnerClose" } })
                    .AddSubStep(new SubStep { Name = "Evaluation", IsFinal = true }, directions: new List<Direction> { new Direction { Command = "Complete", ToStep = "" } });

            workFlowBuilder.CreateStep(new Step { Name = "OwnerClose" })
                    .AddSubStep(new SubStep { Name = "ChooseAlternative", IsFinal = true }, new List<Direction> { new Direction { Command = "UseAlternative" } })
                    .AddSubStep(new SubStep { Name = "ChooseOriginal", IsFinal = true }, new List<Direction> { new Direction { Command = "UseOriginal" } });

            workFlowBuilder.CreateStep(new Step { Name = "Revision" }, new List<Direction> { new Direction { Command = "Send", ToStep = "Approval" } });

            var workFlow = workFlowBuilder.Build();
            Console.WriteLine("Started!!");

            var initialStep = workFlow.InitialStep;
            var step = workFlow.RunRoute(initialStep, "Send");
            step = workFlow.RunRoute(step.TargetStep, "Approve");
            step = workFlow.RunRoute(step.TargetStep, "Approve");
            step = workFlow.RunRoute(step.TargetStep, "Send");
            step = workFlow.RunRoute(step.TargetStep, "UseOriginal");

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        public static bool IsOk(int a)
        {
            return a > 4;
        }

        public static bool Calculate(int i)
        {
            return i > 1;
        }

        public static bool GiveMeSuggestion()
        {
            var input = Console.ReadLine();
            return !string.IsNullOrEmpty(input);
        }
    }

    public interface IWorkFlow<TProcess>
    {
        TProcess Process { get; }
        Step InitialStep { get; }
        Route GetRoute(string currentStep, string command);
        Route RunRoute(BaseStep step, string command);
    }


    public partial class WorkFlowBuilder<TProcess>
    {
        public TProcess Process { get; }
        public List<Flow> FlowSteps { get; private set; }

        public WorkFlowBuilder(TProcess process)
        {
            FlowSteps = new List<Flow>();
            Process = process;
        }
        public bool CheckFlowRoutes()
        {
            //TODO Check if non valid route exists apply on before build.
            return true;
        }
        public WorkFlow Build()
        {
            var workFlow = new WorkFlow(Process, FlowSteps);
            return workFlow;
        }
        public class WorkFlow : IWorkFlow<TProcess>
        {
            public TProcess Process { get; private set; }
            public Step InitialStep { get; private set; }
            public List<Flow> FlowSteps { get; private set; }

            public WorkFlow(TProcess process, List<Flow> flowSteps)
            {
                Process = process;
                FlowSteps = flowSteps;
                InitialStep = flowSteps.SingleOrDefault(x => x.Step.IsInitial == true).Step;
            }

            private List<Flow> GetFlows(string stepName, string command = "")
            {
                //error on multiple step found 
                return FlowSteps.Where(x => x.Step.Name == stepName && (string.IsNullOrEmpty(command) || (x.Directions?.Any(d => d.Command == command) ?? false))).ToList();
            }
            private Flow GetFlow(string currentStepName, string command)
            {
                var flows = GetFlows(currentStepName, command); // ttt
                return flows?.SingleOrDefault(x => x.Directions.Any(d => d.Command == command)) ?? default;
            }
            private SubFlow GetSubFlow(string currentStepName, string command = "")
            {
                return FlowSteps.SelectMany(x => x.flowSubSteps.Where(s => s.Step.Name == currentStepName && (string.IsNullOrEmpty(command) || s.Directions.Any(d => d.Command == command)))).SingleOrDefault() ?? default;
            }
            private BaseStep GetStep(string currentStepName)
            {
                var flows = GetFlows(currentStepName);

                if (flows == null)
                    return default;

                BaseStep step = flows.SingleOrDefault()?.Step ?? default;

                foreach (var item in flows)
                {
                    if (item.blockSubSteps.Any())
                    {
                        step = item.blockSubSteps?.FirstOrDefault()?.Step;
                        break;
                    }
                }

                return step;
            }
            private BaseStep GetSubStep(string currentStepName, string command)
            {
                BaseStep step = null;

                foreach (var item in FlowSteps)
                {
                    if (item.blockSubSteps.Any())
                    {
                        step = item.blockSubSteps?.SingleOrDefault(x => x.Step.Name == currentStepName && x.Directions.Any(d => d.Command == command))?.Step ?? default;
                        if (step != null)
                            break;
                    }
                    else
                    {
                        step = item.flowSubSteps?.SingleOrDefault(x => x.Step.Name == currentStepName && x.Directions.Any(d => d.Command == command))?.Step ?? default;
                        if (step != null)
                            break;
                    }
                }

                return step;
            }
            private BaseStep GetTargetSubStep(string currentStepName, string command)
            {
                BaseStep step = null;

                foreach (var item in FlowSteps)
                {
                    if (item.blockSubSteps.Any())
                    {
                        step = item.blockSubSteps?.SingleOrDefault(x => x.Step.Name == currentStepName && x.Directions.Any(d => d.Command == command))?.Step ?? default;
                        if (step != null)
                        {
                            if (item.blockSubSteps.Count - 1 == ((BlockStep)step).OrderNo)
                            {
                                //Ana Flowa atla
                                step = GetStep(item.blockSubSteps[((BlockStep)step).OrderNo].Directions.SingleOrDefault(d => d.Command == command).ToStep);
                            }
                            else
                            {
                                step = item.blockSubSteps[((BlockStep)step).OrderNo + 1].Step;
                            }
                            break;
                        }
                    }
                    else
                    {
                        step = item.flowSubSteps?.SingleOrDefault(x => x.Step.Name == currentStepName && x.Directions.Any(d => d.Command == command))?.Step ?? default;
                        if (step != null)
                            break;
                    }
                }

                return step;
            }
            public Route RunRoute(BaseStep step, string command)
            {
                Console.WriteLine($"Running Step: {step.Name} with Command: {command}");
                var route = new Route { SourceStep = step };

                if (step.DenyOnCondition != null && step.DenyOnCondition.Invoke())
                {
                    route.Message = $"Failed DenyOnCondition {step.DenyOnCondition.Method.Name}";
                    Console.WriteLine(route.Message);
                    return route;
                }

                if (step.SkipOnCondition != null && step is BlockStep && step.SkipOnCondition.Invoke())
                {
                    route.Message = $"Passing with SkipOnCondition for {step.Name} with  {step.SkipOnCondition.Method.Name}";
                    Console.WriteLine(route.Message);
                    route = GetRoute(step.Name, command);

                    if (route.TargetStep != null && route.SourceStep is BlockStep && route.TargetStep is BlockStep)
                    {
                        RunRoute(route.TargetStep, command);
                        step = route.TargetStep;
                    }

                    if (route.SourceStep is BlockStep && route.TargetStep is not BlockStep)
                    {
                        return route;
                    }
                }

                return GetRoute(step.Name, command);
            }
            public Route GetRoute(string currentStepName, string command)
            {
                if (string.IsNullOrEmpty(currentStepName) || string.IsNullOrEmpty(command))
                    return default;

                var sourceFlow = GetFlow(currentStepName, command);
                SubFlow subSourceFlow = null;
                if (sourceFlow == null)
                {
                    subSourceFlow = GetSubFlow(currentStepName, command);
                }

                BaseStep sourceStep;
                if ((sourceFlow?.Step ?? null) == null && (subSourceFlow?.Step ?? null) == null)
                {
                    sourceStep = GetSubStep(currentStepName, command);
                }
                else
                {
                    sourceStep = sourceFlow?.Step ?? subSourceFlow?.Step;
                }

                BaseStep targetStep = null;

                if (sourceStep is not Step step || !step.IsFinal)
                {
                    if ((sourceFlow?.Step ?? null) != null)
                    {
                        targetStep = sourceFlow?.Directions?.SingleOrDefault(x => x.Command == command)?.ToStep != null ? (GetStep(sourceFlow.Directions.SingleOrDefault(x => x.Command == command).ToStep) ?? default) : default;
                    }
                    else if ((subSourceFlow?.Step ?? null) != null)
                    {
                        targetStep = subSourceFlow?.Directions?.SingleOrDefault(x => x.Command == command)?.ToStep != null ? (GetStep(subSourceFlow.Directions.SingleOrDefault(x => x.Command == command).ToStep) ?? default) : default;
                    }
                    else
                    {
                        targetStep = GetTargetSubStep(sourceStep.Name, command);
                    }
                }


                return new Route { SourceStep = sourceStep, TargetStep = targetStep };
            }

        }

        public Flow CreateStep(Step flowStep, List<Direction> directions = null)
        {
            if (flowStep.IsInitial && FlowSteps.Any(x => x.Step.IsInitial))
                throw new NotSupportedException("Only one initial step is allowed..");

            if (flowStep.IsInitial && flowStep.SkipOnCondition != null)
            {
                throw new NotSupportedException($"Initial step cannot have a skip condition - {flowStep.Name}");
            }

            var flow = new Flow(flowStep, directions);
            FlowSteps.Add(flow);
            return flow;
        }
        public class Flow
        {
            public Step Step { get; private set; }
            public string Identifier { get; private set; }
            public List<Direction> Directions { get; private set; }
            public readonly List<SubFlow> flowSubSteps = new();
            public readonly List<SubFlow> blockSubSteps = new();

            public Flow(Step flowStep, List<Direction> directions)
            {
                Directions = directions;
                Identifier = Guid.NewGuid().ToString();
                Step = flowStep;
            }

            public Flow AddSubStep(SubStep flowSubStep, List<Direction> directions = null)
            {
                if (flowSubStep.IsInitial && flowSubSteps.Any(x => ((SubStep)x.Step).IsInitial))
                    throw new NotSupportedException("Only one initial subStep is allowed..");

                if (flowSubStep.SkipOnCondition != null)
                    throw new NotSupportedException($"Flow subset cannot have a skip condition - {flowSubStep.Name}");

                //Overriding substep name with mainflowname for detect
                flowSubStep.Name = Step.Name;

                var subFlow = new SubFlow(flowSubStep, directions);
                flowSubSteps.Add(subFlow);
                return this;
            }

            public Flow AddBlockOfSubStep(BlockStep flowBlockSubStep)
            {
                if ((blockSubSteps == null || !blockSubSteps.Any()) && flowBlockSubStep.SkipOnCondition != null)
                    throw new NotSupportedException($"First block of sub step cannot have a skip condition - {flowBlockSubStep.Name}");
                flowBlockSubStep.OrderNo = blockSubSteps.Count;

                var subFlow = new SubFlow(flowBlockSubStep, Directions);
                blockSubSteps.Add(subFlow);
                return this;
            }
        }
        public class SubFlow
        {
            public BaseStep Step { get; private set; }
            public string Identifier { get; private set; }
            public List<Direction> Directions { get; private set; }
            public SubFlow(BaseStep flowStep, List<Direction> directions)
            {
                Directions = directions;
                Identifier = Guid.NewGuid().ToString();
                Step = flowStep;
            }
        }
    }

    public abstract class BaseStep
    {
        public string Name { get; set; }
        public Func<bool> SkipOnCondition { get; set; }
        public Func<bool> DenyOnCondition { get; set; }
    }

    public class SubStep : Step
    {

    }

    public class Step : BaseStep
    {
        public bool IsInitial { get; set; }
        public bool IsFinal { get; set; }
    }

    public class BlockStep : BaseStep
    {
        public int OrderNo { get; set; }
    }

    public class Direction
    {
        public string Command { get; set; }
        public string ToStep { get; set; }
    }

    public class Route
    {
        public BaseStep SourceStep { get; set; }
        public BaseStep TargetStep { get; set; }
        public string Message { get; set; }
    }

}
