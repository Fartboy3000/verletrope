using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;

public partial class rope : Node2D
{
    /// <summary>
    /// This script creates a rope using a series of Points, each with currentPos and prevPos. 
    /// These points are used in Verlet integration to simulate rope physics.
    /// </summary>

    //-------------------------------------------Initialize--------------------------------------------------
    private Line2D ropeLine;
    [Export] private InputComponent Input;
    [Export] private CharacterBody2D Movement;

    // Gather required nodes from the scene
    private void GatherRequirements()
    {
        ropeLine = GetNode<Line2D>("RopeLine");
    }

    public override void _Ready()
    {
        GatherRequirements();
        pointCount = 32;  // Define the number of points to represent the rope

        InitialPosition();
    }

    //-------------------------------------------Process--------------------------------------------------
    public override void _PhysicsProcess(double delta)
    {
        UpdateEnds();

        UpdatePoints(delta);  // Update positions of all rope points

        UpdateConstraints();  // Enforce constraints to maintain rope length, you run this repeatedly to get a tighter rope
        UpdateConstraints();
        UpdateConstraints();

        UpdateRender();       // Update rope visualization
    }

    //-------------------------------------------Verlet Integration--------------------------------------------------
    [Export] private float ropeLength;
    [Export] private float constraint = 1f;  // Distance between each point
    [Export] private Vector2 gravity = new Vector2(0, 10f);
    private Vector2 _ropeStart;
    private Vector2 _ropeEnd;
    private Vector2 _ropeEndTarget;  // Target position for the end of the rope

    [Export] private float _ropeExtendSpeed = 40f;
    [Export] private float _ropeRetractSpeed = 40f;

    public struct Point
    {
        public Vector2 currentPos;
        public Vector2 prevPos;
    }

    private int pointCount;  // Number of points in the rope
    private List<Point> ropePoints;
    private List<RayCast2D> rayCasts;

    // Initialize the rope with points
    private void InitialPosition()
    {
        ropePoints = new List<Point>(pointCount);
        rayCasts = new List<RayCast2D>(pointCount);

        for (int i = 0; i < pointCount; i++)
        {
            var point = new Point
            {
                currentPos = _ropeStart,
                prevPos = _ropeStart
            };

            ropePoints.Add(point);
            RayCast2D raycast = new RayCast2D();
            AddChild(raycast);
            rayCasts.Add(raycast);
        }
    }

    // Update the start and end points of the rope
    private void UpdateEnds()
    {
        float ropeIsRetractedTresh = 1;

        _ropeStart = Movement.Position + Input._launchPoint;

        if(((PlayerMovement)Movement)._hasGrappleReleased)
        {
            _ropeEndTarget = _ropeStart; 
            if((_ropeEnd - _ropeStart).Length() > ropeIsRetractedTresh)
            {
                _ropeEnd = _ropeEnd.Lerp(_ropeEndTarget, _ropeRetractSpeed * (float)GetProcessDeltaTime());
            }
            else
            {
                ropeLength = 0;
            }
        }
        else if(((PlayerMovement)Movement)._hasGrappled)
        {
            _ropeEndTarget = ((PlayerMovement)Movement)._grapplePoint;
            _ropeEnd = _ropeEnd.Lerp(_ropeEndTarget, _ropeExtendSpeed * (float)GetProcessDeltaTime());
            ropeLength = _ropeStart.DistanceTo(_ropeEnd);
        }
    }

    // Update positions of all rope points using Verlet integration
    private void UpdatePoints(double delta)
    {
        if (ropeLength == 0)
        {
            for (int i = 0; i < pointCount; i++)
            {
                Point point = ropePoints[i];
                point.currentPos = _ropeStart;
                point.prevPos = _ropeStart;
                ropePoints[i] = point;
            }
            return;
        }

        // Update the start and end of the rope
        if (ropePoints.Count > 0)
        {
            Point startPoint = ropePoints[0];
            startPoint.currentPos = _ropeStart;
            startPoint.prevPos = _ropeStart;
            ropePoints[0] = startPoint;
            
            Point endPoint = ropePoints[ropePoints.Count - 1];
            endPoint.currentPos = _ropeEnd;
            endPoint.prevPos = _ropeEnd;
            ropePoints[ropePoints.Count - 1] = endPoint;
        }

        // Update other points of the rope
        for(int i = 1; i < pointCount - 1; i++)
        {
            Point point = ropePoints[i];
            Vector2 tempPos = point.currentPos;
            point.currentPos += (point.currentPos - point.prevPos) + gravity * (float)(delta * delta);
            point.prevPos = tempPos;
            ropePoints[i] = point;
        }

        // Perform raycast checks for each rope point
        for (int i = 0; i < pointCount - 1; i++)
        {
            Point point = ropePoints[i];
            Point nextPoint = ropePoints[i + 1];

            RayCast2D raycast = rayCasts[i];
            raycast.Position = point.currentPos;
            Vector2 targetPosition = nextPoint.currentPos - point.currentPos;
            raycast.TargetPosition = new Vector2(targetPosition.X, targetPosition.Y);
            raycast.Enabled = true;
            
            if (raycast.IsColliding())
            {
                Vector2 collisionPoint = (Vector2)raycast.GetCollisionPoint();
                point.currentPos = collisionPoint;
                point.prevPos = collisionPoint;
                ropePoints[i] = point;
            }
        }
    }

    // Enforce constraints to maintain consistent length between rope points
    private void UpdateConstraints()
    {
        for (int i = 0; i < pointCount - 1; i++)
        {
            Point pointA = ropePoints[i];
            Point pointB = ropePoints[i + 1];

            Vector2 distanceVec = pointB.currentPos - pointA.currentPos;
            float currentDistance = distanceVec.Length();
            float difference = constraint - currentDistance;
            Vector2 correctionVector = distanceVec.Normalized() * difference;

            if (i != 0)
            {
                pointA.currentPos -= correctionVector * 0.5f;
            }
            if (i != pointCount - 2)
            {
                pointB.currentPos += correctionVector * 0.5f;
            }

            ropePoints[i] = pointA;
            ropePoints[i + 1] = pointB;
        }
    }

    // Update the visual representation of the rope
    private void UpdateRender()
    {
        Vector2[] linePoints = new Vector2[ropePoints.Count];

        if (ropeLength == 0)
        {
            ropeLine.Points = new Vector2[0];
            return;
        }

        for (int i = 0; i < ropePoints.Count; i++)
        {
            linePoints[i] = ropePoints[i].currentPos;
        }

        ropeLine.Points = linePoints;
    }
}
