﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Rtc.Internal.Platform.ResourceContract;
using Microsoft.SfB.PlatformService.SDK.Common;
using Microsoft.Rtc.Internal.RestAPI.ResourceModel;
using System.Threading.Tasks;
using System.Net.Http;

namespace Microsoft.SfB.PlatformService.SDK.ClientModel
{
    /// <summary>
    /// Stand for the participant resource
    /// </summary>
    internal class Participant : BasePlatformResource<ParticipantResource, ParticipantCapability>, IParticipant
    {
        #region Private fields

        private EventHandler<ParticipantModalityChangeEventArgs> m_handleParticipantModalityChange;

        #endregion

        #region Public properties

        /// <summary>
        /// Get the uri of participant
        /// </summary>
        public string Uri
        {
            get { return this.PlatformResource?.Uri; }
        }

        /// <summary>
        /// Get the name of participant
        /// </summary>
        public string Name
        {
            get { return this.PlatformResource?.Name; }
        }

        /// <summary>
        /// The participant messaging resource
        /// </summary>
        public IParticipantMessaging ParticipantMessaging { get; private set; }

        #endregion

        #region Constructor

        internal Participant(IRestfulClient restfulClient, ParticipantResource resource, Uri baseUri, Uri resourceUri, Conversation parent)
            : base(restfulClient, resource, baseUri, resourceUri, parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent), "Conversation is required");
            }
        }

        #endregion

        #region Public events

        public event EventHandler<ParticipantModalityChangeEventArgs> HandleParticipantModalityChange
        {
            add
            {
                if (m_handleParticipantModalityChange == null || !m_handleParticipantModalityChange.GetInvocationList().Contains(value))
                {
                    m_handleParticipantModalityChange += value;
                }
            }
            remove { m_handleParticipantModalityChange -= value; }
        }

        #endregion

        #region Public methods

        public override bool Supports(ParticipantCapability capability)
        {
            string href = null;

            switch (capability)
            {
                case ParticipantCapability.Eject:
                    {
                        href = PlatformResource?.EjectParticipantOperationLink?.Href;
                        break;
                    }
            }

            return !string.IsNullOrEmpty(href);
        }

        public async Task EjectAsync(LoggingContext loggingContext)
        {
            string href = PlatformResource?.EjectParticipantOperationLink?.Href;

            if(string.IsNullOrWhiteSpace(href))
            {
                throw new CapabilityNotAvailableException("Link to eject participant is not available.");
            }

            Uri url = UriHelper.CreateAbsoluteUri(BaseUri, href);
            var content = new StringContent(string.Empty);

            await PostRelatedPlatformResourceAsync(url, content, loggingContext).ConfigureAwait(false);
        }

        #endregion

        #region Internal methods

        /// <summary>
        /// ProcessAndDispatchEventsToChild : For child resources here
        /// </summary>
        /// <param name="eventcontext"></param>
        /// <returns></returns>
        internal override bool ProcessAndDispatchEventsToChild(EventContext eventcontext)
        {
            //No child to dispatch any more, need to dispatch to child , process locally for message type
            if (string.Equals(eventcontext.EventEntity.Link.Token, TokenMapper.GetTokenName(typeof(ParticipantMessagingResource))))
            {
                if (eventcontext.EventEntity.Relationship == EventOperation.Added)
                {
                    ParticipantMessagingResource participantMessaging = this.ConvertToPlatformServiceResource<ParticipantMessagingResource>(eventcontext);
                    if (participantMessaging != null)
                    {
                        ParticipantMessaging = new ParticipantMessaging(this.RestfulClient, participantMessaging, this.BaseUri,
                               UriHelper.CreateAbsoluteUri(eventcontext.BaseUri, participantMessaging.SelfUri), this);
                    }

                    m_handleParticipantModalityChange?.Invoke(this, new ParticipantModalityChangeEventArgs
                    {
                        AddedModalities = new List<EventableEntity> { ParticipantMessaging as ParticipantMessaging }

                    });
                }

                if (eventcontext.EventEntity.Relationship == EventOperation.Deleted)
                {
                    m_handleParticipantModalityChange?.Invoke(this, new ParticipantModalityChangeEventArgs
                    {
                        RemovedModalities = new List<EventableEntity> { ParticipantMessaging as ParticipantMessaging }

                    });

                    ParticipantMessaging = null;
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
    }

    public class ParticipantModalityChangeEventArgs : EventArgs
    {
        public List<EventableEntity> AddedModalities { get; internal set; }
        public List<EventableEntity> RemovedModalities { get; internal set; }
    }
}
