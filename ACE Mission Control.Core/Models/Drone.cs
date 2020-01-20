using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class Drone
    {
        public int ID;
        public string Name;
        public OnboardComputerClient OBCClient;

        public Drone(int id, string name, string clientHostname, string clientUsername)
        {
            ID = id;
            Name = name;
            OBCClient = new OnboardComputerClient(this, clientHostname, clientUsername);
        }
    }
}
