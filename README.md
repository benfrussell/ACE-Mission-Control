# ACE-Mission-Control

A Windows application that connects to ACE drone onboard computer software. Automation Coordination and Execution of drone missions.

This program primarily bridges UgCS (Universal Ground Control Software) and ACE Onboard Computer. ACE Onboard Computer runs on an onboard computer hardwired to the drone flight controller and communicates with ACE Mission Control over the local network.

## Functions
* Automatically retrieve area scan routes and waypoint routes from UgCS and translate them into payload operation instructions that are automatically synchronized with a connected onboard computer.
* Provide tools for customizing the payload operation instructions and the configuration of the onboard computer.
* Process telemetry logs exported from UgCS to summarize flight statistics.
