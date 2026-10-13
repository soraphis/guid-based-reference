using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace UnityEngine.GUID {
    public class GuidReferenceTests {
        // Tests - make a new GUID
        // duplicate it
        // make it a prefab
        // delete it
        // reference it
        // dereference it

        string prefabPath;
        GuidComponent guidBase;
        GameObject prefab;
        GuidComponent guidPrefab;

        [OneTimeSetUp]
        public void Setup () {
            prefabPath = "Assets/TemporaryTestGuid.prefab";

            guidBase = CreateNewGuid();
            prefab = PrefabUtility.SaveAsPrefabAsset(guidBase.gameObject, prefabPath);

            guidPrefab = prefab.GetComponent<GuidComponent>();
        }

        public GuidComponent CreateNewGuid () {
            GameObject newGO = new GameObject("GuidTestGO");
            return newGO.AddComponent<GuidComponent>();
        }

        [UnityTest]
        public IEnumerator GuidCreation () {
            GuidComponent guid1 = guidBase;
            GuidComponent guid2 = CreateNewGuid();

            Assert.AreNotEqual(guid1.GetGuid(), guid2.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetGuidUpdatesRegistrationAndSerializedData () {
            System.Guid previousGuid = guidBase.GetGuid();
            System.Guid assignedGuid = System.Guid.NewGuid();
            GuidReference previousReference = new GuidReference(guidBase);
            Assert.AreSame(guidBase.gameObject, previousReference.gameObject);

            guidBase.SetGuid(assignedGuid);

            FieldInfo serializedGuidField = typeof(GuidComponent).GetField("serializedGuid", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(serializedGuidField);
            CollectionAssert.AreEqual(assignedGuid.ToByteArray(), serializedGuidField.GetValue(guidBase));
            Assert.IsNull(previousReference.gameObject);
            Assert.AreSame(guidBase.gameObject, GuidManager.ResolveGuid(assignedGuid));
            Assert.IsNull(GuidManager.ResolveGuid(previousGuid));

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetGuidRejectsEmptyGuidWithoutChangingState () {
            System.Guid previousGuid = guidBase.GetGuid();
            LogAssert.Expect(LogType.Error, "Cannot assign an empty GUID.");

            guidBase.SetGuid(System.Guid.Empty);

            Assert.AreEqual(previousGuid, guidBase.GetGuid());
            Assert.AreSame(guidBase.gameObject, GuidManager.ResolveGuid(previousGuid));

            yield return null;
        }

        [UnityTest]
        public IEnumerator SetGuidRejectsCollisionWithoutChangingState () {
            GuidComponent other = CreateNewGuid();
            System.Guid previousGuid = guidBase.GetGuid();
            System.Guid takenGuid = other.GetGuid();
            string expectedError = $"the guid {System.Text.RegularExpressions.Regex.Escape(takenGuid.ToString())} cannot be assigned.*because it is already taken.*";
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(expectedError));

            guidBase.SetGuid(takenGuid);

            Assert.AreEqual(previousGuid, guidBase.GetGuid());
            Assert.AreSame(guidBase.gameObject, GuidManager.ResolveGuid(previousGuid));
            Assert.AreSame(other.gameObject, GuidManager.ResolveGuid(takenGuid));

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidDuplication () {
            LogAssert.Expect(LogType.Warning, "Guid Collision Detected while creating GuidTestGO(Clone).\nAssigning new Guid.");

            GuidComponent clone = GameObject.Instantiate<GuidComponent>(guidBase);

            Assert.AreNotEqual(guidBase.GetGuid(), clone.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidPrefab () {
            Assert.AreNotEqual(guidBase.GetGuid(), guidPrefab.GetGuid());
            Assert.AreEqual(guidPrefab.GetGuid(), System.Guid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidPrefabContentsHaveNoGuid () {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

            try {
                GuidComponent prefabContentsGuid = prefabContents.GetComponent<GuidComponent>();
                Assert.AreEqual(System.Guid.Empty, prefabContentsGuid.GetGuid());
            } finally {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidSurvivesTemporarilyClearedSerializedData () {
            System.Guid originalGuid = guidBase.GetGuid();
            FieldInfo serializedGuidField = typeof(GuidComponent).GetField("serializedGuid", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo onValidate = typeof(GuidComponent).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(serializedGuidField);
            Assert.IsNotNull(onValidate);
            serializedGuidField.SetValue(guidBase, null);
            onValidate.Invoke(guidBase, null);

            Assert.AreEqual(originalGuid, guidBase.GetGuid());
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidPrefabInstance () {
            GuidComponent instance = GameObject.Instantiate<GuidComponent>(guidPrefab);
            Assert.AreNotEqual(guidBase.GetGuid(), instance.GetGuid());
            Assert.AreNotEqual(instance.GetGuid(), guidPrefab.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidValidReference () {
            GuidReference reference = new GuidReference(guidBase);
            Assert.AreEqual(reference.gameObject, guidBase.gameObject);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuidReferenceDistinguishesSameTypeComponents () {
            GameObject target = new GameObject("ComponentGuidTarget");

            try {
                GuidComponent owner = target.AddComponent<GuidComponent>();
                BoxCollider first = target.AddComponent<BoxCollider>();
                BoxCollider second = target.AddComponent<BoxCollider>();
                System.Guid firstComponentGuid = owner.GetGuid(first);
                GuidReference<BoxCollider> firstReference = new GuidReference<BoxCollider>(first);
                GuidReference<BoxCollider> secondReference = new GuidReference<BoxCollider>(second);

                Assert.AreNotEqual(firstReference.Component.GetInstanceID(), secondReference.Component.GetInstanceID());
                Assert.AreSame(first, firstReference.Component);
                Assert.AreSame(second, secondReference.Component);
                Assert.AreSame(target, firstReference.gameObject);

                firstReference.OnBeforeSerialize();
                FieldInfo serializedGuidField = typeof(GuidReference<BoxCollider>).GetField("serializedGuid", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(serializedGuidField);
                GuidReference<BoxCollider> restoredReference = new GuidReference<BoxCollider>();
                serializedGuidField.SetValue(restoredReference, ((byte[])serializedGuidField.GetValue(firstReference)).Clone());
                restoredReference.OnAfterDeserialize();
                Assert.AreSame(first, restoredReference.Component);

                owner.OnBeforeSerialize();
                owner.OnAfterDeserialize();
                Assert.AreEqual(firstComponentGuid, owner.GetGuid(first));
            } finally {
                Object.DestroyImmediate(target);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuidSurvivesTemporarilyClearedSerializedData () {
            GameObject target = new GameObject("ComponentGuidSerializationTarget");

            try {
                GuidComponent owner = target.AddComponent<GuidComponent>();
                BoxCollider collider = target.AddComponent<BoxCollider>();
                System.Guid originalGuid = owner.GetGuid(collider);

                FieldInfo entriesField = typeof(GuidComponent).GetField("componentGuids", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo serializedGuidField = typeof(ComponentGuid).GetField("serializedGuid", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onAfterDeserialize = typeof(ComponentGuid).GetMethod("OnAfterDeserialize", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.IsNotNull(entriesField);
                Assert.IsNotNull(serializedGuidField);
                Assert.IsNotNull(onAfterDeserialize);

                List<ComponentGuid> entries = (List<ComponentGuid>)entriesField.GetValue(owner);
                ComponentGuid componentGuid = entries[0];
                serializedGuidField.SetValue(componentGuid, null);
                onAfterDeserialize.Invoke(componentGuid, null);

                Assert.AreEqual(originalGuid, componentGuid.Guid);
                CollectionAssert.AreEqual(originalGuid.ToByteArray(), serializedGuidField.GetValue(componentGuid));
                Assert.AreEqual(originalGuid, owner.GetGuid(collider));
            } finally {
                Object.DestroyImmediate(target);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuidUndoRestoresIdentityAndRedoRemovesIt () {
            GameObject target = new GameObject("ComponentGuidUndoTarget");
            GuidComponent owner = target.AddComponent<GuidComponent>();
            BoxCollider collider = target.AddComponent<BoxCollider>();
            System.Guid originalGuid = owner.GetGuid(collider);
            GuidReference<BoxCollider> reference = new GuidReference<BoxCollider>(collider);

            try {
                Undo.DestroyObjectImmediate(collider);
                Assert.IsNull(reference.Component);

                Undo.PerformUndo();
                yield return null;

                BoxCollider restored = target.GetComponent<BoxCollider>();
                Assert.IsNotNull(restored);
                Assert.AreEqual(originalGuid, owner.GetGuid(restored));
                Assert.AreSame(restored, reference.Component);

                Undo.PerformRedo();
                Assert.IsNull(reference.Component);
            } finally {
                Undo.ClearAll();
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator RecreatedComponentReceivesNewGuid () {
            GameObject target = new GameObject("RecreatedComponentTarget");

            try {
                GuidComponent owner = target.AddComponent<GuidComponent>();
                BoxCollider original = target.AddComponent<BoxCollider>();
                System.Guid originalGuid = owner.GetGuid(original);

                Object.DestroyImmediate(original);
                BoxCollider replacement = target.AddComponent<BoxCollider>();
                System.Guid replacementGuid = owner.GetGuid(replacement);

                Assert.AreNotEqual(originalGuid, replacementGuid);
                Assert.AreSame(replacement, owner.GetComponentFromGuid(replacementGuid));
                Assert.IsNull(owner.GetComponentFromGuid(originalGuid));

                ComponentGuid orphan = null;
                foreach (ComponentGuid entry in owner.GetComponentGuids()) {
                    if (entry.Guid == originalGuid) {
                        orphan = entry;
                        break;
                    }
                }

                Assert.IsNotNull(orphan);
                Assert.IsTrue(owner.ForgetOrphanedComponentGuid(orphan));
                Assert.IsFalse(owner.GetComponentGuids().Contains(orphan));
            } finally {
                Object.DestroyImmediate(target);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuidReferenceNotifiesWhenComponentIsDestroyed () {
            GameObject target = new GameObject("DestroyedComponentTarget");
            target.AddComponent<GuidComponent>();
            BoxCollider collider = target.AddComponent<BoxCollider>();
            GuidReference<BoxCollider> reference = new GuidReference<BoxCollider>(collider);
            int removedCount = 0;
            reference.OnGuidRemoved += () => {
                removedCount++;
                Assert.IsNull(reference.Component);
            };

            Object.DestroyImmediate(collider);

            Assert.IsNull(reference.Component);
            Assert.AreEqual(1, removedCount);
            Object.DestroyImmediate(target);

            yield return null;
        }

        [UnityTest]
        public IEnumerator GuidInvalidReference () {
            GuidComponent newGuid = CreateNewGuid();
            GuidReference reference = new GuidReference(newGuid);
            Object.DestroyImmediate(newGuid);

            Assert.IsNull(reference.gameObject);

            yield return null;
        }

        [OneTimeTearDown]
        public void TearDown () {
            AssetDatabase.DeleteAsset(prefabPath);
        }
    }
}
