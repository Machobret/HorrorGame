#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Reflection;
using HorrorEngine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TwoSidedDoorTestLock : DoorLock
{
    public int NormalAttempts;
    public int ExitAttempts;
    protected override void Awake() { }
    public override void OnTryToUnlock(out bool openImmediately)
    {
        ++NormalAttempts;
        openImmediately = false;
    }
    public override void OnTryToUnlockFromExit(out bool openImmediately)
    {
        ++ExitAttempts;
        openImmediately = false;
    }
}

public sealed class TwoSidedDoorTests
{
    private GameObject m_Root;
    private TwoSidedDoor m_Door;
    private PlayerInteractor m_User;

    [SetUp]
    public void SetUp()
    {
        m_Root = new GameObject("TwoSidedDoor test fixture");
        m_Door = Child("Door").AddComponent<TwoSidedDoor>();
        m_Door.ExitPoint = Child("Side A").transform;
        m_Door.ExitPoint.position = Vector3.back * 2f;
        m_Door.OtherExitPoint = Child("Side B").transform;
        m_Door.OtherExitPoint.position = Vector3.forward * 2f;
        m_User = CreateInteractor("Interactor");
        m_Door.OnOpened = new UnityEvent();
        m_Door.OnLocked = new UnityEvent();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(m_Root);
    }

    private GameObject Child(string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(m_Root.transform);
        return child;
    }

    private PlayerInteractor CreateInteractor(string name)
    {
        var child = Child(name);
        child.SetActive(false);
        var interactor = child.AddComponent<PlayerInteractor>();
        // These tests invoke Use directly, without the player's input/detector setup.
        interactor.enabled = false;
        child.SetActive(true);
        return interactor;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private object Invoke(string name, params object[] args)
    {
        return typeof(TwoSidedDoor).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(m_Door, args);
    }

    [TestCase(-1f, true)]
    [TestCase(1f, false)]
    [TestCase(0f, true)]
    public void SelectsFartherExitWithDeterministicTie(float z, bool sideB)
    {
        Assert.That(Invoke("SelectDestination", Vector3.forward * z),
            Is.SameAs(sideB ? m_Door.OtherExitPoint : m_Door.ExitPoint));
    }

    [TestCase(-1f, 1, 0)]
    [TestCase(1f, 0, 1)]
    public void RoutesLockedAttemptsBySideAndAllowsRetry(float z, int normal, int exit)
    {
        var doorLock = m_Door.gameObject.AddComponent<TwoSidedDoorTestLock>();
        SetField(m_Door, "m_Lock", doorLock);
        m_User.transform.position = Vector3.forward * z;
        int locked = 0;
        int opened = 0;
        m_Door.OnLocked.AddListener(() => { ++locked; m_Door.Use(m_User); });
        m_Door.OnOpened.AddListener(() => ++opened);
        m_Door.Use(m_User);
        m_Door.Use(m_User);
        Assert.That(doorLock.NormalAttempts, Is.EqualTo(normal * 2));
        Assert.That(doorLock.ExitAttempts, Is.EqualTo(exit * 2));
        Assert.That(locked, Is.EqualTo(2));
        Assert.That(opened, Is.Zero);
        Assert.That(m_Door.IsLocked(), Is.True);
    }

    [Test]
    public void RejectsMissingOrOverlappingExitsWithoutOpening()
    {
        m_Door.OtherExitPoint.position = m_Door.ExitPoint.position;
        LogAssert.Expect(LogType.Error, "TwoSidedDoor requires two distinct exit points on opposite sides of the doorway.");
        m_Door.Use(m_User);
        m_Door.OtherExitPoint = null;
        LogAssert.Expect(LogType.Error, "TwoSidedDoor requires two distinct exit points on opposite sides of the doorway.");
        m_Door.Use(m_User);
    }

    [Test]
    public void TeleportUsesActorEventAndDestinationRotation()
    {
        var actor = m_User.gameObject.AddComponent<Actor>();
        actor.OnTeleported = new UnityEvent();
        int teleports = 0;
        actor.OnTeleported.AddListener(() => ++teleports);
        typeof(DoorBase).GetField("m_Interactor", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(m_Door, actor.transform);
        m_Door.OtherExitPoint.rotation = Quaternion.Euler(0, 135, 0);
        var routine = (IEnumerator)Invoke("TransitionRoutine", m_Door.OtherExitPoint);
        routine.MoveNext();
        Assert.That(actor.transform.position, Is.EqualTo(m_Door.OtherExitPoint.position));
        Assert.That(Quaternion.Angle(actor.transform.rotation, m_Door.OtherExitPoint.rotation), Is.LessThan(0.01f));
        Assert.That(teleports, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FullTransitionPublishesEngineMessagesAndSupportsReturnTrip()
    {
        // The normal controller needs Play Mode for fades and its coroutines.
        Object.DestroyImmediate(m_Root);
        yield return new EnterPlayMode();
        yield return RunRoundTrip();
        yield return new ExitPlayMode();
    }

    private IEnumerator RunRoundTrip()
    {
        // Allocate callback closures after the domain reload; the test runner
        // cannot preserve compiler-generated closure objects across EnterPlayMode.
        var root = new GameObject("Runtime door fixture");
        System.Func<string, GameObject> create = name =>
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform);
            return child;
        };
        var door = create("Door").AddComponent<TwoSidedDoor>();
        door.ExitPoint = create("Side A").transform;
        door.ExitPoint.position = Vector3.back * 2f;
        door.OtherExitPoint = create("Side B").transform;
        door.OtherExitPoint.position = Vector3.forward * 2f;
        door.OnOpened = new UnityEvent();
        door.OnLocked = new UnityEvent();

        var ui = create("UI");
        ui.SetActive(false);
        ui.AddComponent<UIManager>();
        var image = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        image.transform.SetParent(ui.transform);
        var fade = image.AddComponent<UIFade>();
        SetField(fade, "m_Fade", image.GetComponent<Image>());
        ui.SetActive(true);

        var controllerObject = create("Transition controller");
        controllerObject.AddComponent<AudioSource>();
        var controller = controllerObject.AddComponent<DoorTransitionController>();
        SetField(controller, "m_PauseGame", false);
        SetField(controller, "m_FadeOutDuration", 0.01f);
        SetField(controller, "m_FadeInDuration", 0.01f);

        var actor = create("Actor").AddComponent<Actor>();
        actor.OnTeleported = new UnityEvent();
        // Attach after Actor.Awake so its enable/disable list does not include the
        // input component: this fixture has no player input/detector setup.
        // Put the interactor on a child to exercise actor-root resolution.
        var userObject = create("Child interactor");
        userObject.SetActive(false);
        var childUser = userObject.AddComponent<PlayerInteractor>();
        childUser.enabled = false;
        userObject.SetActive(true);
        childUser.transform.SetParent(actor.transform);
        childUser.transform.localPosition = Vector3.forward * 10f;
        actor.transform.position = Vector3.back;
        int opened = 0, teleported = 0, midway = 0, post = 0, ended = 0;
        door.OnOpened.AddListener(() => { ++opened; door.Use(childUser); });
        actor.OnTeleported.AddListener(() => ++teleported);
        MessageBuffer<DoorTransitionMidWayMessage>.MessageCallback onMid = msg =>
        {
            if (msg.Door != door) return;
            ++midway;
            Assert.That(msg.User, Is.SameAs(actor.gameObject));
            Assert.That(actor.IsDisabled, Is.True);
            // The destination must already be captured when listeners move the actor.
            actor.transform.position = Vector3.forward;
        };
        MessageBuffer<DoorTransitionMidWayPostRoutineMessage>.MessageCallback onPost = msg => { if (msg.Door == door) ++post; };
        MessageBuffer<DoorTransitionEndMessage>.MessageCallback onEnd = msg => { if (msg.Door == door) ++ended; };
        MessageBuffer<DoorTransitionMidWayMessage>.Subscribe(onMid);
        MessageBuffer<DoorTransitionMidWayPostRoutineMessage>.Subscribe(onPost);
        MessageBuffer<DoorTransitionEndMessage>.Subscribe(onEnd);
        // Unity does not resume WaitForEndOfFrame in batch mode. Substitute a
        // normal frame yield only in this fixture, and restore the engine cache.
        var endOfFrameField = typeof(Yielders).GetField("_endOfFrame", BindingFlags.Static | BindingFlags.NonPublic);
        object endOfFrame = endOfFrameField.GetValue(null);
        if (Application.isBatchMode)
            endOfFrameField.SetValue(null, null);
        try
        {
            yield return null; // Let the controller find UIFade in Start.
            for (int trip = 0; trip < 2; ++trip)
            {
                door.Use(childUser);
                door.Use(childUser);
                float deadline = Time.realtimeSinceStartup + 5f;
                while (ended <= trip && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(ended, Is.EqualTo(trip + 1), "Transition did not finish");
                Assert.That(actor.transform.position, Is.EqualTo(trip == 0 ? door.OtherExitPoint.position : door.ExitPoint.position));
                Assert.That(actor.IsDisabled, Is.False);
            }
            Assert.That(opened, Is.EqualTo(2));
            Assert.That(teleported, Is.EqualTo(2));
            Assert.That(midway, Is.EqualTo(2));
            Assert.That(post, Is.EqualTo(2));
        }
        finally
        {
            endOfFrameField.SetValue(null, endOfFrame);
            MessageBuffer<DoorTransitionMidWayMessage>.Unsubscribe(onMid);
            MessageBuffer<DoorTransitionMidWayPostRoutineMessage>.Unsubscribe(onPost);
            MessageBuffer<DoorTransitionEndMessage>.Unsubscribe(onEnd);
            Object.DestroyImmediate(root);
        }
    }
}
#endif
