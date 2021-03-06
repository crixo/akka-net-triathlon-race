﻿using Akka.Actor;
using Messages;
using System.Collections.Generic;
using System.Linq;

namespace Actors
{
    /// <summary>
    /// Actor that handles race control.
    /// </summary>
    public class RaceControlActor : UntypedActor
    {
        //private RoadInfo _roadInfo;
        private List<string> _athletes = new List<string>();

        public RaceControlActor()
        {
            // initialize state
            //_roadInfo = roadInfo;
        }

        /// <summary>
        /// Handle received message.
        /// </summary>
        /// <param name="message">The message to handle.</param>
        protected override void OnReceive(object message)
        {
            switch(message)
            {
                case RaceClosed ver:
                    Handle(ver);
                    break;
                case AthleteRegistered ver:
                    Handle(ver);
                    break;
                case AthleteEntryRegistered ver:
                    Handle(ver);
                    break;
                case AthleteCheckRegistered ver:
                    Handle(ver);
                    break;
                case AthleteExitRegistered vxr:
                    Handle(vxr);
                    break;            
            }
        }

        private void Handle(Test msg)
        {
            _athletes.Add(msg.BibId);
            FluentConsole.White.Line($"Path: {Self.Path}, Athletes: {_athletes.Count}");
        }

        private void Handle(AthleteRegistered msg)
        {
            //var props = Props.Create<AthleteActor>(msg.BibId);
            var props = Props.Create<AthleteSwitchableActor>(msg.BibId);
            var athleteActor = Context.ActorOf(props, $"athlete-{msg.BibId}");
        }

        /// <summary>
        /// Handle VehicleEntryRegistered message.
        /// </summary>
        /// <param name="msg">The message to handle.</param>
        private void Handle(AthleteEntryRegistered msg)
        {
            //ICanTell athleteActor;

            //if ((int)msg.Gate == 1)
            //{
            //    var props = Props.Create<AthleteActor>(msg.BibId);
            //    athleteActor = Context.ActorOf(props, $"athlete-{msg.BibId}");
            //}
            //else
            //{
            //    athleteActor = Context.ActorSelection($"/user/race-control/*/athlete-{msg.BibId}");
            //}
            var athleteActor = Context.ActorSelection($"/user/race-control/*/athlete-{msg.BibId}");

            athleteActor.Tell(msg, Self);

        }

        /// <summary>
        /// Handle VehicleExitRegistered message.
        /// </summary>
        /// <param name="msg">The message to handle.</param>
        private void Handle(AthleteExitRegistered msg)
        {
            var athleteActor = Context.ActorSelection($"/user/race-control/*/athlete-{msg.BibId}");
            athleteActor.Tell(msg);
        }

        private void Handle(AthleteCheckRegistered msg)
        {
            //var athleteActor = Context.ActorSelection($"/user/race-control/*/athlete-{msg.BibId}");
            //athleteActor.Tell(msg);
            var standingActor = Context.ActorSelection($"/user/standing");
            standingActor.Tell(msg);
        }

        private void Handle(RaceClosed msg)
        {
            var athleteActor = Context.ActorSelection($"/user/race-control/*/*");
            athleteActor.Tell(new RaceClosed());
        }
    }
}
