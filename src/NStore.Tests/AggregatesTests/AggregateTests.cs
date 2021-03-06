﻿using System;
using NStore.Aggregates;
using NStore.SnapshotStore;
using Xunit;

namespace NStore.Tests.AggregatesTests
{
    public class AggregateTests
    {
        [Fact]
        public void new_aggregate_should_not_be_itialized()
        {
            var ticket = new Ticket();

            Assert.False(ticket.IsInitialized);
            Assert.Equal(0, ticket.Version);
            Assert.False(ticket.IsDirty);
            Assert.Null(ticket.ExposedStateForTest);
        }

        [Fact]
        public void calling_init_more_than_once_should_throw_()
        {
            var ticket = new Ticket();
            ticket.Init("abc");

            var ex = Assert.Throws<AggregateAlreadyInitializedException>(() => ticket.Init("bce"));
            Assert.Equal("abc", ex.AggregateId);
            Assert.Equal(typeof(Ticket), ex.AggregateType);
        }

        [Theory()]
        [InlineData(null)]
        [InlineData("")]
        public void cannot_init_with_invalid_id(string id)
        {
            var ticket = new Ticket();
            Assert.Throws<ArgumentNullException>(() => ticket.Init(id));
        }

        [Fact]
        public void init_without_params_should_create_default_state()
        {
            var ticket = new Ticket();
            ticket.Init("new_ticket");

            Assert.NotNull(ticket.ExposedStateForTest);
            Assert.Equal("new_ticket", ticket.Id);
        }

        [Fact]
        public void apply_changes_should_be_idempotent()
        {
            Ticket ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;
            var changeSet = new Changeset(1, new TicketSold());

            persister.ApplyChanges(changeSet);
            persister.ApplyChanges(changeSet);

            Assert.True(ticket.IsInitialized);
            Assert.Equal(1, ticket.Version);
            Assert.False(ticket.IsDirty);
        }


        [Fact]
        public void append_should_increase_version()
        {
            Ticket ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;
            var changeSet = new Changeset(1, new TicketSold());
            persister.ApplyChanges(changeSet);

            Assert.True(ticket.IsInitialized);
            Assert.Equal(1, ticket.Version);
            Assert.False(ticket.IsDirty);
        }

        [Fact]
        public void raising_event_should_not_increase_version()
        {
            Ticket ticket = TicketTestFactory.ForTest();

            ticket.Sale();

            var changes = ticket.ExposePendingChanges();

            Assert.Equal(0, ticket.Version);
            Assert.True(ticket.IsDirty);
            Assert.IsType<TicketSold>(changes.Events[0]);
            Assert.True(ticket.ExposedStateForTest.HasBeenSold);
        }

        [Fact]
        public void aggregate_without_changes_should_build_an_empty_changeset()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;
            var changeSet = persister.GetChangeSet();

            Assert.NotNull(changeSet);
            Assert.True(changeSet.IsEmpty);
        }

        [Fact]
        public void persister_should_create_changeset_with_new_events()
        {
            var ticket = TicketTestFactory.Sold();
            var persister = (IEventSourcedAggregate)ticket;
            var changeSet = persister.GetChangeSet();

            Assert.NotNull(changeSet);
            Assert.False(changeSet.IsEmpty);
            Assert.Equal(1, changeSet.AggregateVersion);
        }

        [Fact]
        public void persister_should_create_changeset_only_with_new_events()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;

            var changeSet = new Changeset(1, new TicketSold());
            persister.ApplyChanges(changeSet);

            ticket.Refund();

            changeSet = persister.GetChangeSet();

            Assert.NotNull(changeSet);
            Assert.False(changeSet.IsEmpty);
            Assert.Equal(2, changeSet.AggregateVersion);
            Assert.Equal(1, changeSet.Events.Length);
            Assert.IsType<TicketRefunded>(changeSet.Events[0]);
        }

        [Fact]
        public void changes_must_be_applied_in_strict_order()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;

            var first = new Changeset(1, new TicketSold());
            var third = new Changeset(3, new TicketSold());

            persister.ApplyChanges(first);

            var ex = Assert.Throws<AggregateRestoreException>(() =>
            {
                persister.ApplyChanges(third);
            });

            Assert.Equal(2, ex.ExpectedVersion);
            Assert.Equal(3, ex.RestoreVersion);
        }

        [Fact]
        public void restoring_null_snapshot_should_throw()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;

            Assert.Throws<ArgumentNullException>(() =>
            {
                persister.TryRestore(null);
            });
        }

        [Fact]
        public void restoring_empty_snapshot_should_return_false()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;
            var snapshot = new SnapshotInfo(null, 0, null, 0);
            var restored = persister.TryRestore(snapshot);

            Assert.False(restored);
        }

        [Fact]
        public void restoring_incompatible_snapshot_should_return_false()
        {
            var ticket = TicketTestFactory.ForTest();
            var persister = (IEventSourcedAggregate)ticket;
            var snapshot = new SnapshotInfo(ticket.Id, 2, null, 0);
            var restored = persister.TryRestore(snapshot);

            Assert.False(restored);
        }
    }
}