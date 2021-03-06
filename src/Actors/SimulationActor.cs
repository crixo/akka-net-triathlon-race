﻿using Akka.Actor;
using Messages;
using System;
using System.Collections.Generic;

namespace Actors
{
    public class ExitDelayInSecond
    {
        public int Min { get; set; }

        public int Max { get; set; }
    }

    public class GateInfo
    {
        public ExitDelayInSecond ExitDelay { get; set; }

        public int NrOfIntermediateChecks { get; set; }
    }


    /// <summary>
    /// Actor that simulates traffic.
    /// </summary>
    public class SimulationActor : UntypedActor
    {
        private int _numberOfAthletes;
        private int _atheltesSimulated;
        private string _randomWinner;
        private string _randomDisqualified;
        private Random _rnd;
        private TimeSpan _raceDuration = TimeSpan.FromSeconds(20);

        private int _minEntryDelayInMS = 50;
        private int _maxEntryDelayInMS = 5000;
        private int _minTransitionDelayInS = 1;
        private int _maxTransitionDelayInS = 2;
        private Dictionary<Gates, GateInfo> _exitDelay;
        //private Dictionary<Gates, GateInfo> _exitDelay = new Dictionary<Gates, GateInfo>()
        //{
        //    //{ Gates.Swim.ToString(), new ExitDelayInSecond{Min = 8, Max = 16} },
        //    //{ Gates.Bike.ToString(), new ExitDelayInSecond{Min = 25, Max = 40} },
        //    //{ Gates.Run.ToString(), new ExitDelayInSecond{Min = 14, Max = 30} },
        //    { Gates.Swim, new GateInfo(){ ExitDelay = new ExitDelayInSecond{Min = 1, Max = 2}, NrOfIntermediateChecks = 0 } },
        //    { Gates.Bike,  new GateInfo(){ ExitDelay = new ExitDelayInSecond{Min = 3, Max = 4}, NrOfIntermediateChecks = 2 } },
        //    { Gates.Run,  new GateInfo(){ ExitDelay = new ExitDelayInSecond{Min = 2, Max = 3}, NrOfIntermediateChecks = 1 } },
        //};

        public SimulationActor(Dictionary<Gates, GateInfo> gates)
        {
            _exitDelay = gates;
        }

        /// <summary>
        /// Handle received message.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case TestSimulation ss:
                    Handle(ss);
                    break;
                case StartSimulation ss:
                    Handle(ss);
                    break;
                case SimulatePassingAthlete spc:
                    Handle(spc);
                    break;
                case Shutdown sd:
                    Context.Stop(Self);
                    break;
            }
        }

        private void Handle(TestSimulation msg)
        {
            var raceControl = Context.System.ActorSelection($"/user/race-control");


            for (int i = 0; i < msg.NumberOfAthletes; i++)
            {
                raceControl.Tell(new Test(i.ToString()));
            }
        }

        /// <summary>
        /// Handle StartSimulation message.
        /// </summary>
        /// <param name="msg">The message to handle.</param>
        private void Handle(StartSimulation msg)
        {
            // initialize state
            _numberOfAthletes = msg.NumberOfAthletes;
            _atheltesSimulated = 0;
            _rnd = new Random();

            // start simulationloop
            //var simulatePassingCar = new SimulatePassingCar(GenerateRandomLicenseNumber());
            //Context.System.Scheduler.ScheduleTellOnce(
            //    _rnd.Next(_minEntryDelayInMS, _maxEntryDelayInMS), Self, simulatePassingCar, Self);

            var raceStartedAt = DateTime.Now;
            _randomWinner = _rnd.Next(1, msg.NumberOfAthletes).ToString();
            _randomDisqualified = _randomWinner;
            while (_randomDisqualified == _randomWinner)
            {
                _randomDisqualified = _rnd.Next(1, msg.NumberOfAthletes).ToString();
            }

            FluentConsole.Magenta.Line($"Winner should be #{_randomWinner}");
            FluentConsole.Magenta.Line($"Disqualified should be #{_randomDisqualified}");

            var raceControl = Context.System.ActorSelection($"/user/race-control");
            for (int i = 0; i < msg.NumberOfAthletes; i++)
            {
                var bibId = (i + 1).ToString();
                raceControl.Tell(new AthleteRegistered(bibId));
            }

            var standingActor = Context.System.ActorSelection($"/user/standing");
            standingActor.Tell(new RaceStarted(raceStartedAt));



            for (int i = 0; i < msg.NumberOfAthletes; i++)
            {
                var bibId = (i + 1).ToString();
                Self.Tell(new SimulatePassingAthlete(bibId, raceStartedAt));
            }

            var standingBikeActor = Context.System.ActorSelection($"/user/standing-bike");
            Context.System.Scheduler.ScheduleTellOnce(_raceDuration.Add(TimeSpan.FromSeconds(1)), standingBikeActor, new Shutdown(), Self);


            Context.System.Scheduler.ScheduleTellOnce(_raceDuration,
                raceControl,
                new RaceClosed(),
                Self);


            Context.System.Scheduler.ScheduleTellOnce(_raceDuration,
                standingActor,
                new PrintFinalStanding(10),
                Self);

        }

        private void Handle(SimulatePassingAthlete msg)
        {
            _atheltesSimulated++;
            var isWinner = msg.BibId == _randomWinner;

            DateTime entryTimestamp = DateTime.Now;// msg.RaceStartedAt;
            TimeSpan delay = TimeSpan.FromSeconds(0);
            var counter = 1;
            foreach (var kv in _exitDelay)
            {
                var athletePasedAsEntered = new AthletePassed(msg.BibId, entryTimestamp, kv.Key);
                ActorSelection entryGate = Context.System.ActorSelection($"/user/entrygate{kv.Key.ToString().ToLower()}");
                Context.System.Scheduler.ScheduleTellOnce(
                    computeMessageDelay(entryTimestamp), //delay
                    entryGate, 
                    athletePasedAsEntered, 
                    Self);
                //Console.WriteLine("Athlete {0} entered gate {1} at {2}", msg.BibId, kv.Key, entryTimestamp.ToString("HH:mm:ss.ffffff"));

                var gateDelay = !isWinner ?
                    TimeSpan.FromSeconds(_rnd.Next(kv.Value.ExitDelay.Min, kv.Value.ExitDelay.Max) + _rnd.NextDouble())
                    : TimeSpan.FromSeconds(kv.Value.ExitDelay.Min);
                //Console.WriteLine("Athlete #{0} - gate:{1} - gateDelay {2}", msg.BibId, kv.Key, gateDelay);

                for (int i = 0; i < kv.Value.NrOfIntermediateChecks; i++)
                {
                    ActorSelection gate = Context.System.ActorSelection($"/user/intermediategate-{kv.Key.ToString().ToLower()}-{i+1}");
                    var intermediateFractionDelay = gateDelay.TotalMilliseconds / (kv.Value.NrOfIntermediateChecks + 1);
                    //Console.WriteLine("Athlete #{0} - gate:{1} - intermediateFractionDelay {2}", msg.BibId, kv.Key, intermediateFractionDelay);
                    var intermediateDelay = TimeSpan.FromMilliseconds(intermediateFractionDelay * (i+1));
                    //Console.WriteLine("Athlete #{0} - gate:{1} - intermediateDelay {2}", msg.BibId, kv.Key, intermediateDelay);
                    var intermediateTimestamp = entryTimestamp.Add(intermediateDelay);
                    var athletePasedIntermediateCheck = new AthletePassed(msg.BibId, intermediateTimestamp, kv.Key);
                    //Console.WriteLine("Athlete {0} intermediate {1} at {2}", msg.BibId, totalIntermediateGate, intermediateTimestamp.ToString("HH:mm:ss.ffffff"));
                    Context.System.Scheduler.ScheduleTellOnce(
                        computeMessageDelay(intermediateTimestamp),
                        gate,
                        athletePasedIntermediateCheck, 
                        Self);
                }

                //delay = delay + gateDelay;
                //DateTime exitTimestamp = entryTimestamp.Add(delay);
                //var athletePasedAsExited = new AthletePassed(msg.BibId, exitTimestamp, kv.Key);
                DateTime exitTimestamp = entryTimestamp.Add(gateDelay);
                var athletePassedAsExited = new AthletePassed(msg.BibId, exitTimestamp, kv.Key);
                ActorSelection exityGate = Context.System.ActorSelection($"/user/exitgate{kv.Key.ToString().ToLower()}");
                if (msg.BibId == _randomDisqualified && kv.Key == Gates.Run)
                {
                    FluentConsole.Magenta.Line($"Athlete #{_randomDisqualified} should be disqualified for missing exiting gate {kv.Key}");
                }
                else
                {
                    Context.System.Scheduler.ScheduleTellOnce(
                        computeMessageDelay(exitTimestamp), //delay, 
                        exityGate, 
                        athletePassedAsExited, 
                        Self);
                    //Console.WriteLine("Athlete {0} exited gate {1} w/ delay {2}", msg.BibId, counter, delay.TotalSeconds);
                    //Console.WriteLine("Athlete {0} exited gate {1} at {2}", msg.BibId, kv.Key, exitTimestamp.ToString("HH:mm:ss.ffffff"));
                }

                if (counter < _exitDelay.Count)
                {
                    var transitionTime = !isWinner ?
                        TimeSpan.FromSeconds(_rnd.Next(_minTransitionDelayInS, _minTransitionDelayInS) + _rnd.NextDouble())
                        : TimeSpan.FromSeconds(_minTransitionDelayInS);
                    entryTimestamp = exitTimestamp.Add(transitionTime);
                    //delay = delay + transitionTime;
                }

                counter++;
            }

            if (_atheltesSimulated == _numberOfAthletes)
            {
                Self.Tell(new Shutdown());
            }
        }

        private TimeSpan computeMessageDelay(DateTime timespan)
        {
            var delay = timespan.Subtract(DateTime.Now);
            return delay.TotalMilliseconds > 0? delay : TimeSpan.FromMilliseconds(0);
        }
    }
}
