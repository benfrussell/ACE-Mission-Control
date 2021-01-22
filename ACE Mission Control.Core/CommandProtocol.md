### ACE Command Protocol

#### Mission Control <--> Director

##### Diagnostics

Command				| Description
---					| ---
ping				| Test for a "pong" response.
check_errors		| List any errors in the error log.
clear_errors		| Clear the error log.
sleep_test			| Freeze the director for 10 seconds to test connection drops.
test_interface		| Attempt to switch the interface into 'active mode' for a limited duration.
test_payload		| 
stop_test			|

##### Mission Config

Command				| Description
---					| ---
set_mission			|
add_area			|
set_fly_through		|
set_entry			|
set_duration		|
set_payload			|
check_mission_config|

##### Mission Control

Command				| Description
---					| 
activate_mission	|
deactivate_mission	|
reset_mission		|
check_mission_status|

##### Drone Control

Command				| Description
---					| 
start_interface		|
check_interface		|
force_return_home	|
