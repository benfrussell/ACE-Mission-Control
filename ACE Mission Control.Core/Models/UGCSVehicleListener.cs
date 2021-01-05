using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class UGCSVehicleListener : UGCSServerObjectListener
    {
        private EventSubscriptionWrapper _eventSubscriptionWrapper;
        private ObjectModificationSubscription _objectNotificationSubscription;
        private Dictionary<int, System.Action<ModificationType, Vehicle>> _vehicleList = new Dictionary<int, System.Action<ModificationType, Vehicle>>();
        private int _clientID;
        private MessageExecutor _executor;

        /// <summary>
        /// Add vehicle to listener
        /// </summary>
        /// <param name="vehicleId">vehicle id</param>
        /// <param name="callBack">callback for listener</param>
        private void AddVehicleIdTolistener(int vehicleId, System.Action<ModificationType, Vehicle> callBack)
        {
            if (!_vehicleList.ContainsKey(vehicleId))
            {
                _vehicleList.Add(vehicleId, callBack);
            }
        }

        public UGCSVehicleListener(EventSubscriptionWrapper espw, int clientID, MessageExecutor executor, NotificationListener notificationListener)
        {
            _eventSubscriptionWrapper = espw;
            _notificationListener = notificationListener;
            _clientID = clientID;
            _executor = executor;
        }

        /// <summary>
        /// Example how to activate subscription o vehicle modifications
        /// </summary>
        /// <param name="es">ObjectModification with vehicle object id</param>
        /// <param name="callBack">callback with received event</param>
        public void SubscribeVehicle(ObjectModificationSubscription es, System.Action<ModificationType, Vehicle> callBack)
        {
            _objectNotificationSubscription = es;
            _eventSubscriptionWrapper.ObjectModificationSubscription = _objectNotificationSubscription;

            SubscribeEventRequest requestEvent = new SubscribeEventRequest();
            requestEvent.ClientId = _clientID;

            requestEvent.Subscription = _eventSubscriptionWrapper;

            var responce = _executor.Submit<SubscribeEventResponse>(requestEvent);
            var subscribeEventResponse = responce.Value;

            SubscriptionToken st = new SubscriptionToken(
                subscribeEventResponse.SubscriptionId, 
                _getObjectNotificationHandler<Vehicle>(
                    (token, exception, vehicle) => { _messageReceived(vehicle, token, _vehicleList[vehicle.Id]); }
                ), 
                _eventSubscriptionWrapper
            );
            _notificationListener.AddSubscription(st);
            tokens.Add(st);
            AddVehicleIdTolistener(es.ObjectId, callBack);
           
        }

        /// <summary>
        /// Point where modificated vehicle object is received
        /// </summary>
        /// <param name="vehicle">modificated vehicle object</param>
        /// <param name="callback">callback for modificated vehcile object</param>
        public void _messageReceived(Vehicle vehicle, ModificationType modification, System.Action<ModificationType, Vehicle> callback)
        {
            callback(modification, vehicle);
        }
    }
}
