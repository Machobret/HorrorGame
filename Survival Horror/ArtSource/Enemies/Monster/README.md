# Monster

The Blender master is Monster_Rig.blend. Frame 1 is the rest pose. Frame 25 shows inward finger curl and a deformation check.

Use Monster_Encounter.prefab from Assets/Game/Enemies/Monster for level placement. Place the prefab on a baked NavMesh. The prefab inherits the Retro Horror Template monster's combat and save components and uses its own model, Animator Controller, and six Unity animation clips. The head hitbox follows the animated head.

The Chapter 0 Tutorial uses the same prefab through TutorialMonster.prefab. That wrapper disables the agent because the Tutorial sequence drives movement during the aiming and shooting lesson.
