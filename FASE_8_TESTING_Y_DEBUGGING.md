# 🧪 FASE 8: TESTING Y DEBUGGING (Día 10)

## 🎯 **OBJETIVO DE LA FASE**
Implementar un **sistema completo de testing y debugging** para validar todos los componentes de AI desarrollados, asegurar que cumplan con los requisitos del TP, y crear herramientas para demostrar el funcionamiento.

---

## 📋 **¿QUÉ BUSCAMOS LOGRAR?**

### **Problema Actual:**
- Múltiples sistemas complejos sin validación integrada
- No hay forma fácil de demostrar que cumple requisitos del TP
- Debugging manual y poco eficiente
- Falta visibilización del comportamiento de AI

### **Solución con Testing Completo:**
- **Test Suite automatizado** para todos los sistemas
- **Herramientas de debugging visual** en tiempo real
- **Métricas de rendimiento** y estadísticas
- **Demo modes** para mostrar cada requisito del TP

---

## 🔧 **SISTEMA DE TESTING AUTOMATIZADO**

### **AITestSuite.cs - Manager Principal de Tests**
```csharp
public class AITestSuite : MonoBehaviour
{
    [Header("Test Configuration")]
    [SerializeField] private bool runTestsOnStart = false;
    [SerializeField] private bool enableContinuousValidation = true;
    [SerializeField] private float validationInterval = 5f;
    [SerializeField] private AITestConfiguration testConfig;
    
    [Header("Test Targets")]
    [SerializeField] private List<GameObject> testTargets = new List<GameObject>();
    [SerializeField] private Transform playerTransform;
    
    [Header("Results Display")]
    [SerializeField] private AITestReportDisplay reportDisplay;
    [SerializeField] private bool logResultsToConsole = true;
    [SerializeField] private bool saveResultsToFile = false;
    
    // Test managers
    private FSMTestManager fsmTester;
    private SteeringTestManager steeringTester;
    private LineOfSightTestManager losTester;
    private DecisionTreeTestManager decisionTreeTester;
    private RouletteWheelTestManager rouletteTester;
    private BlackboardTestManager blackboardTester;
    private IntegrationTestManager integrationTester;
    
    // Test results
    private Dictionary<string, TestResult> lastTestResults = new Dictionary<string, TestResult>();
    private List<TestSession> testHistory = new List<TestSession>();
    private AITestReport currentReport;
    
    // Continuous validation
    private float lastValidationTime;
    private Coroutine continuousValidationCoroutine;
    
    void Start()
    {
        InitializeTestManagers();
        
        if (runTestsOnStart)
        {
            StartCoroutine(RunAllTestsCoroutine());
        }
        
        if (enableContinuousValidation)
        {
            StartContinuousValidation();
        }
    }
    
    private void InitializeTestManagers()
    {
        fsmTester = new FSMTestManager(testConfig.fsmTestConfig);
        steeringTester = new SteeringTestManager(testConfig.steeringTestConfig);
        losTester = new LineOfSightTestManager(testConfig.losTestConfig);
        decisionTreeTester = new DecisionTreeTestManager(testConfig.decisionTreeTestConfig);
        rouletteTester = new RouletteWheelTestManager(testConfig.rouletteTestConfig);
        blackboardTester = new BlackboardTestManager(testConfig.blackboardTestConfig);
        integrationTester = new IntegrationTestManager(testConfig.integrationTestConfig);
        
        Logger.LogDebug("AITestSuite: All test managers initialized");
    }
    
    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        StartCoroutine(RunAllTestsCoroutine());
    }
    
    private IEnumerator RunAllTestsCoroutine()
    {
        Logger.LogInfo("AITestSuite: Starting comprehensive test suite...");
        
        var session = new TestSession
        {
            startTime = Time.time,
            testResults = new Dictionary<string, TestResult>()
        };
        
        // Run individual system tests
        yield return StartCoroutine(RunSystemTests(session));
        
        // Run integration tests
        yield return StartCoroutine(RunIntegrationTests(session));
        
        // Generate final report
        session.endTime = Time.time;
        session.duration = session.endTime - session.startTime;
        
        GenerateTestReport(session);
        
        testHistory.Add(session);
        
        // Cleanup and limit history size
        if (testHistory.Count > 10)
        {
            testHistory.RemoveAt(0);
        }
        
        Logger.LogInfo($"AITestSuite: Test suite completed in {session.duration:F2}s");
    }
    
    private IEnumerator RunSystemTests(TestSession session)
    {
        Logger.LogInfo("Running individual system tests...");
        
        // Test FSM System
        yield return StartCoroutine(RunFSMTests(session));
        
        // Test Steering Behaviors
        yield return StartCoroutine(RunSteeringTests(session));
        
        // Test Line of Sight
        yield return StartCoroutine(RunLineOfSightTests(session));
        
        // Test Decision Trees
        yield return StartCoroutine(RunDecisionTreeTests(session));
        
        // Test Roulette Wheel Selection
        yield return StartCoroutine(RunRouletteWheelTests(session));
        
        // Test Blackboard System
        yield return StartCoroutine(RunBlackboardTests(session));
    }
    
    private IEnumerator RunFSMTests(TestSession session)
    {
        Logger.LogDebug("Testing FSM System...");
        
        var guards = FindObjectsOfType<Guard>();
        if (guards.Length == 0)
        {
            session.testResults["FSM_NoGuards"] = TestResult.CreateFailure("No guards found for FSM testing");
            yield break;
        }
        
        foreach (var guard in guards)
        {
            // Test state transitions
            var transitionTest = fsmTester.TestStateTransitions(guard);
            session.testResults[$"FSM_StateTransitions_{guard.name}"] = transitionTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test state data consistency
            var dataTest = fsmTester.TestStateDataConsistency(guard);
            session.testResults[$"FSM_StateData_{guard.name}"] = dataTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test FSM performance
            var performanceTest = fsmTester.TestPerformance(guard);
            session.testResults[$"FSM_Performance_{guard.name}"] = performanceTest;
            yield return new WaitForSeconds(0.1f);
        }
        
        Logger.LogDebug($"FSM tests completed for {guards.Length} guards");
    }
    
    private IEnumerator RunSteeringTests(TestSession session)
    {
        Logger.LogDebug("Testing Steering Behaviors...");
        
        var aiControllers = FindObjectsOfType<AIMovementController>();
        if (aiControllers.Length == 0)
        {
            session.testResults["Steering_NoControllers"] = TestResult.CreateFailure("No AI movement controllers found");
            yield break;
        }
        
        foreach (var controller in aiControllers)
        {
            // Test individual behaviors
            var seekTest = steeringTester.TestSeekBehavior(controller);
            session.testResults[$"Steering_Seek_{controller.name}"] = seekTest;
            yield return new WaitForSeconds(0.2f);
            
            var fleeTest = steeringTester.TestFleeBehavior(controller);
            session.testResults[$"Steering_Flee_{controller.name}"] = fleeTest;
            yield return new WaitForSeconds(0.2f);
            
            var obstacleTest = steeringTester.TestObstacleAvoidance(controller);
            session.testResults[$"Steering_Obstacle_{controller.name}"] = obstacleTest;
            yield return new WaitForSeconds(0.2f);
            
            // Test behavior combination
            var combinationTest = steeringTester.TestBehaviorCombination(controller);
            session.testResults[$"Steering_Combination_{controller.name}"] = combinationTest;
            yield return new WaitForSeconds(0.2f);
        }
        
        Logger.LogDebug($"Steering tests completed for {aiControllers.Length} controllers");
    }
    
    private IEnumerator RunLineOfSightTests(TestSession session)
    {
        Logger.LogDebug("Testing Line of Sight System...");
        
        var playerDetectors = FindObjectsOfType<PlayerDetector>();
        if (playerDetectors.Length == 0)
        {
            session.testResults["LOS_NoDetectors"] = TestResult.CreateFailure("No player detectors found");
            yield break;
        }
        
        foreach (var detector in playerDetectors)
        {
            // Test basic detection
            var basicTest = losTester.TestBasicDetection(detector, playerTransform);
            session.testResults[$"LOS_Basic_{detector.name}"] = basicTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test field of view
            var fovTest = losTester.TestFieldOfView(detector, playerTransform);
            session.testResults[$"LOS_FOV_{detector.name}"] = fovTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test obstruction detection
            var obstructionTest = losTester.TestObstructionDetection(detector, playerTransform);
            session.testResults[$"LOS_Obstruction_{detector.name}"] = obstructionTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test performance under stress
            var stressTest = losTester.TestPerformanceStress(detector);
            session.testResults[$"LOS_Stress_{detector.name}"] = stressTest;
            yield return new WaitForSeconds(0.1f);
        }
        
        Logger.LogDebug($"Line of Sight tests completed for {playerDetectors.Length} detectors");
    }
    
    private IEnumerator RunDecisionTreeTests(TestSession session)
    {
        Logger.LogDebug("Testing Decision Tree System...");
        
        var civilians = FindObjectsOfType<CivilianAI>();
        if (civilians.Length == 0)
        {
            session.testResults["DT_NoCivilians"] = TestResult.CreateFailure("No civilians found for decision tree testing");
            yield break;
        }
        
        foreach (var civilian in civilians)
        {
            // Test decision tree evaluation
            var evaluationTest = decisionTreeTester.TestDecisionEvaluation(civilian);
            session.testResults[$"DT_Evaluation_{civilian.name}"] = evaluationTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test decision consistency
            var consistencyTest = decisionTreeTester.TestDecisionConsistency(civilian);
            session.testResults[$"DT_Consistency_{civilian.name}"] = consistencyTest;
            yield return new WaitForSeconds(0.5f); // Longer wait for consistency test
            
            // Test tree performance
            var performanceTest = decisionTreeTester.TestTreePerformance(civilian);
            session.testResults[$"DT_Performance_{civilian.name}"] = performanceTest;
            yield return new WaitForSeconds(0.1f);
        }
        
        Logger.LogDebug($"Decision Tree tests completed for {civilians.Length} civilians");
    }
    
    private IEnumerator RunRouletteWheelTests(TestSession session)
    {
        Logger.LogDebug("Testing Roulette Wheel Selection...");
        
        var personalityControllers = FindPersonalityControllers();
        if (personalityControllers.Count == 0)
        {
            session.testResults["RW_NoControllers"] = TestResult.CreateFailure("No personality controllers found");
            yield break;
        }
        
        foreach (var controller in personalityControllers)
        {
            // Test distribution accuracy
            var distributionTest = rouletteTester.TestDistributionAccuracy(controller);
            session.testResults[$"RW_Distribution_{controller.GetGuardName()}"] = distributionTest;
            yield return new WaitForSeconds(0.1f);
            
            // Test adaptation mechanism
            var adaptationTest = rouletteTester.TestAdaptationMechanism(controller);
            session.testResults[$"RW_Adaptation_{controller.GetGuardName()}"] = adaptationTest;
            yield return new WaitForSeconds(0.2f);
            
            // Test weight persistence
            var persistenceTest = rouletteTester.TestWeightPersistence(controller);
            session.testResults[$"RW_Persistence_{controller.GetGuardName()}"] = persistenceTest;
            yield return new WaitForSeconds(0.1f);
        }
        
        Logger.LogDebug($"Roulette Wheel tests completed for {personalityControllers.Count} controllers");
    }
    
    private IEnumerator RunBlackboardTests(TestSession session)
    {
        Logger.LogDebug("Testing Blackboard System...");
        
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            session.testResults["BB_NoBlackboard"] = TestResult.CreateFailure("Blackboard service not found");
            yield break;
        }
        
        // Test basic operations
        var basicTest = blackboardTester.TestBasicOperations(blackboard);
        session.testResults["BB_BasicOperations"] = basicTest;
        yield return new WaitForSeconds(0.1f);
        
        // Test data consistency
        var consistencyTest = blackboardTester.TestDataConsistency(blackboard);
        session.testResults["BB_DataConsistency"] = consistencyTest;
        yield return new WaitForSeconds(0.1f);
        
        // Test performance
        var performanceTest = blackboardTester.TestPerformance(blackboard);
        session.testResults["BB_Performance"] = performanceTest;
        yield return new WaitForSeconds(0.1f);
        
        // Test memory management
        var memoryTest = blackboardTester.TestMemoryManagement(blackboard);
        session.testResults["BB_MemoryManagement"] = memoryTest;
        yield return new WaitForSeconds(0.1f);
        
        Logger.LogDebug("Blackboard tests completed");
    }
    
    private IEnumerator RunIntegrationTests(TestSession session)
    {
        Logger.LogDebug("Running integration tests...");
        
        // Test Guard-Civilian coordination
        var coordinationTest = integrationTester.TestGuardCivilianCoordination();
        session.testResults["Integration_GuardCivilianCoordination"] = coordinationTest;
        yield return new WaitForSeconds(1f);
        
        // Test Blackboard integration
        var blackboardIntegrationTest = integrationTester.TestBlackboardIntegration();
        session.testResults["Integration_BlackboardUsage"] = blackboardIntegrationTest;
        yield return new WaitForSeconds(0.5f);
        
        // Test AI performance under load
        var loadTest = integrationTester.TestAIPerformanceUnderLoad();
        session.testResults["Integration_PerformanceLoad"] = loadTest;
        yield return new WaitForSeconds(2f);
        
        // Test TP requirements compliance
        var complianceTest = integrationTester.TestTPRequirementsCompliance();
        session.testResults["Integration_TPCompliance"] = complianceTest;
        yield return new WaitForSeconds(0.5f);
        
        Logger.LogDebug("Integration tests completed");
    }
    
    private void GenerateTestReport(TestSession session)
    {
        currentReport = new AITestReport
        {
            session = session,
            overallScore = CalculateOverallScore(session),
            criticalFailures = GetCriticalFailures(session),
            recommendations = GenerateRecommendations(session),
            tpRequirementStatus = EvaluateTPRequirements(session),
            timestamp = System.DateTime.Now
        };
        
        if (reportDisplay != null)
        {
            reportDisplay.DisplayReport(currentReport);
        }
        
        if (logResultsToConsole)
        {
            LogReportToConsole(currentReport);
        }
        
        if (saveResultsToFile)
        {
            SaveReportToFile(currentReport);
        }
    }
    
    private float CalculateOverallScore(TestSession session)
    {
        if (session.testResults.Count == 0) return 0f;
        
        int totalTests = session.testResults.Count;
        int passedTests = session.testResults.Values.Count(r => r.passed);
        
        float baseScore = (float)passedTests / totalTests;
        
        // Apply weights for critical systems
        var criticalTests = session.testResults.Where(kvp => 
            kvp.Key.Contains("Integration_TPCompliance") ||
            kvp.Key.Contains("FSM_") ||
            kvp.Key.Contains("DT_") ||
            kvp.Key.Contains("RW_")).ToList();
        
        if (criticalTests.Count > 0)
        {
            int criticalPassed = criticalTests.Count(kvp => kvp.Value.passed);
            float criticalScore = (float)criticalPassed / criticalTests.Count;
            
            // Weight critical tests more heavily
            baseScore = (baseScore * 0.6f) + (criticalScore * 0.4f);
        }
        
        return Mathf.Clamp01(baseScore);
    }
    
    private List<string> GetCriticalFailures(TestSession session)
    {
        var failures = new List<string>();
        
        foreach (var kvp in session.testResults)
        {
            if (!kvp.Value.passed && kvp.Value.severity == TestSeverity.Critical)
            {
                failures.Add($"{kvp.Key}: {kvp.Value.message}");
            }
        }
        
        return failures;
    }
    
    private List<string> GenerateRecommendations(TestSession session)
    {
        var recommendations = new List<string>();
        
        // Analyze patterns in test failures
        var failedTests = session.testResults.Where(kvp => !kvp.Value.passed).ToList();
        
        if (failedTests.Any(kvp => kvp.Key.Contains("Performance")))
        {
            recommendations.Add("Consider optimizing AI update loops and reducing computation frequency");
        }
        
        if (failedTests.Any(kvp => kvp.Key.Contains("FSM")))
        {
            recommendations.Add("Review FSM state transitions and ensure proper state data management");
        }
        
        if (failedTests.Any(kvp => kvp.Key.Contains("Steering")))
        {
            recommendations.Add("Check steering behavior weights and ensure smooth movement transitions");
        }
        
        if (failedTests.Any(kvp => kvp.Key.Contains("DT")))
        {
            recommendations.Add("Validate decision tree structure and node evaluation logic");
        }
        
        if (recommendations.Count == 0)
        {
            recommendations.Add("All systems are functioning well. Consider adding more advanced features.");
        }
        
        return recommendations;
    }
    
    private TPRequirementStatus EvaluateTPRequirements(TestSession session)
    {
        var status = new TPRequirementStatus();
        
        // Check FSM requirement
        status.fsmImplemented = session.testResults.Any(kvp => 
            kvp.Key.Contains("FSM") && kvp.Value.passed);
        
        // Check Steering Behaviors requirement
        status.steeringBehaviorsImplemented = session.testResults.Any(kvp => 
            kvp.Key.Contains("Steering") && kvp.Value.passed);
        
        // Check Line of Sight requirement
        status.lineOfSightImplemented = session.testResults.Any(kvp => 
            kvp.Key.Contains("LOS") && kvp.Value.passed);
        
        // Check Decision Trees requirement
        status.decisionTreesImplemented = session.testResults.Any(kvp => 
            kvp.Key.Contains("DT") && kvp.Value.passed);
        
        // Check Roulette Wheel Selection requirement
        status.rouletteWheelImplemented = session.testResults.Any(kvp => 
            kvp.Key.Contains("RW") && kvp.Value.passed);
        
        // Check for two groups of enemies
        var guards = FindObjectsOfType<Guard>();
        var civilians = FindObjectsOfType<CivilianAI>();
        status.twoEnemyGroupsImplemented = guards.Length > 0 && civilians.Length > 0;
        
        // Check for aggressive/conservative personalities
        var personalities = FindPersonalityControllers();
        status.personalitiesImplemented = personalities.Any(p => p.IsAggressiveType()) && 
                                         personalities.Any(p => p.IsConservativeType());
        
        return status;
    }
    
    private List<PersonalityController> FindPersonalityControllers()
    {
        var controllers = new List<PersonalityController>();
        var guards = FindObjectsOfType<Guard>();
        
        foreach (var guard in guards)
        {
            var controller = guard.GetPersonalityController();
            if (controller != null)
            {
                controllers.Add(controller);
            }
        }
        
        return controllers;
    }
    
    private void StartContinuousValidation()
    {
        if (continuousValidationCoroutine != null)
        {
            StopCoroutine(continuousValidationCoroutine);
        }
        
        continuousValidationCoroutine = StartCoroutine(ContinuousValidationLoop());
    }
    
    private IEnumerator ContinuousValidationLoop()
    {
        while (enableContinuousValidation)
        {
            yield return new WaitForSeconds(validationInterval);
            
            // Run quick validation tests
            RunQuickValidation();
        }
    }
    
    private void RunQuickValidation()
    {
        // Quick checks for critical systems
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null)
        {
            Logger.LogError("AITestSuite: Blackboard service lost during runtime!");
            return;
        }
        
        // Check if AI entities are still functioning
        var guards = FindObjectsOfType<Guard>();
        var civilians = FindObjectsOfType<CivilianAI>();
        
        if (guards.Length == 0 && civilians.Length == 0)
        {
            Logger.LogWarning("AITestSuite: No AI entities found during validation!");
            return;
        }
        
        Logger.LogDebug($"AITestSuite: Quick validation passed - {guards.Length} guards, {civilians.Length} civilians");
    }
    
    private void LogReportToConsole(AITestReport report)
    {
        Debug.Log("=== AI TEST SUITE REPORT ===");
        Debug.Log($"Overall Score: {report.overallScore:P1}");
        Debug.Log($"Tests Run: {report.session.testResults.Count}");
        Debug.Log($"Duration: {report.session.duration:F2}s");
        
        if (report.criticalFailures.Count > 0)
        {
            Debug.LogError("Critical Failures:");
            foreach (var failure in report.criticalFailures)
            {
                Debug.LogError($"  - {failure}");
            }
        }
        
        Debug.Log("TP Requirements Status:");
        Debug.Log($"  FSM: {(report.tpRequirementStatus.fsmImplemented ? "✓" : "✗")}");
        Debug.Log($"  Steering: {(report.tpRequirementStatus.steeringBehaviorsImplemented ? "✓" : "✗")}");
        Debug.Log($"  Line of Sight: {(report.tpRequirementStatus.lineOfSightImplemented ? "✓" : "✗")}");
        Debug.Log($"  Decision Trees: {(report.tpRequirementStatus.decisionTreesImplemented ? "✓" : "✗")}");
        Debug.Log($"  Roulette Wheel: {(report.tpRequirementStatus.rouletteWheelImplemented ? "✓" : "✗")}");
        Debug.Log($"  Two Enemy Groups: {(report.tpRequirementStatus.twoEnemyGroupsImplemented ? "✓" : "✗")}");
        Debug.Log($"  Personalities: {(report.tpRequirementStatus.personalitiesImplemented ? "✓" : "✗")}");
        
        Debug.Log("=== END REPORT ===");
    }
    
    private void SaveReportToFile(AITestReport report)
    {
        string fileName = $"AI_Test_Report_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        
        try
        {
            string jsonContent = JsonUtility.ToJson(report, true);
            System.IO.File.WriteAllText(filePath, jsonContent);
            
            Logger.LogInfo($"AITestSuite: Report saved to {filePath}");
        }
        catch (System.Exception e)
        {
            Logger.LogError($"AITestSuite: Failed to save report - {e.Message}");
        }
    }
    
    void OnDestroy()
    {
        if (continuousValidationCoroutine != null)
        {
            StopCoroutine(continuousValidationCoroutine);
        }
    }
    
    #region Public API for External Testing
    
    [ContextMenu("Test FSM Only")]
    public void TestFSMOnly()
    {
        StartCoroutine(TestSpecificSystem("FSM"));
    }
    
    [ContextMenu("Test Steering Only")]
    public void TestSteeringOnly()
    {
        StartCoroutine(TestSpecificSystem("Steering"));
    }
    
    [ContextMenu("Test Decision Trees Only")]
    public void TestDecisionTreesOnly()
    {
        StartCoroutine(TestSpecificSystem("DecisionTrees"));
    }
    
    [ContextMenu("Validate TP Requirements")]
    public void ValidateTPRequirements()
    {
        var dummySession = new TestSession { testResults = new Dictionary<string, TestResult>() };
        
        // Quick TP validation
        var status = EvaluateTPRequirements(dummySession);
        
        Debug.Log("=== TP REQUIREMENTS VALIDATION ===");
        Debug.Log($"FSM: {(status.fsmImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Steering Behaviors: {(status.steeringBehaviorsImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Line of Sight: {(status.lineOfSightImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Decision Trees: {(status.decisionTreesImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Roulette Wheel: {(status.rouletteWheelImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Two Enemy Groups: {(status.twoEnemyGroupsImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        Debug.Log($"Personalities: {(status.personalitiesImplemented ? "✓ IMPLEMENTED" : "✗ MISSING")}");
        
        bool allRequirementsMet = status.fsmImplemented && 
                                 status.steeringBehaviorsImplemented && 
                                 status.lineOfSightImplemented && 
                                 status.decisionTreesImplemented && 
                                 status.rouletteWheelImplemented && 
                                 status.twoEnemyGroupsImplemented && 
                                 status.personalitiesImplemented;
        
        Debug.Log($"=== OVERALL TP STATUS: {(allRequirementsMet ? "✓ ALL REQUIREMENTS MET" : "✗ REQUIREMENTS MISSING")} ===");
    }
    
    private IEnumerator TestSpecificSystem(string systemName)
    {
        Logger.LogInfo($"Testing {systemName} system...");
        
        var session = new TestSession
        {
            startTime = Time.time,
            testResults = new Dictionary<string, TestResult>()
        };
        
        switch (systemName)
        {
            case "FSM":
                yield return StartCoroutine(RunFSMTests(session));
                break;
            case "Steering":
                yield return StartCoroutine(RunSteeringTests(session));
                break;
            case "DecisionTrees":
                yield return StartCoroutine(RunDecisionTreeTests(session));
                break;
        }
        
        session.endTime = Time.time;
        session.duration = session.endTime - session.startTime;
        
        LogReportToConsole(new AITestReport { session = session, overallScore = CalculateOverallScore(session) });
    }
    
    #endregion
}

// Supporting classes and structs
[System.Serializable]
public class TestSession
{
    public float startTime;
    public float endTime;
    public float duration;
    public Dictionary<string, TestResult> testResults;
}

[System.Serializable]
public struct TestResult
{
    public bool passed;
    public string message;
    public float executionTime;
    public TestSeverity severity;
    public Dictionary<string, object> additionalData;
    
    public static TestResult CreateSuccess(string message, float executionTime = 0f)
    {
        return new TestResult
        {
            passed = true,
            message = message,
            executionTime = executionTime,
            severity = TestSeverity.Info,
            additionalData = new Dictionary<string, object>()
        };
    }
    
    public static TestResult CreateFailure(string message, TestSeverity severity = TestSeverity.Error)
    {
        return new TestResult
        {
            passed = false,
            message = message,
            executionTime = 0f,
            severity = severity,
            additionalData = new Dictionary<string, object>()
        };
    }
}

public enum TestSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

[System.Serializable]
public struct AITestReport
{
    public TestSession session;
    public float overallScore;
    public List<string> criticalFailures;
    public List<string> recommendations;
    public TPRequirementStatus tpRequirementStatus;
    public System.DateTime timestamp;
}

[System.Serializable]
public struct TPRequirementStatus
{
    public bool fsmImplemented;
    public bool steeringBehaviorsImplemented;
    public bool lineOfSightImplemented;
    public bool decisionTreesImplemented;
    public bool rouletteWheelImplemented;
    public bool twoEnemyGroupsImplemented;
    public bool personalitiesImplemented;
}
```

---

## 🎯 **HERRAMIENTAS DE DEBUGGING VISUAL**

### **AIDebugVisualizer.cs - Visualización en Tiempo Real**
```csharp
public class AIDebugVisualizer : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private bool enableVisualization = true;
    [SerializeField] private AIDebugMode debugMode = AIDebugMode.Overview;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showGUI = true;
    
    [Header("Visual Styles")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float lineWidth = 0.1f;
    [SerializeField] private Color guardColor = Color.red;
    [SerializeField] private Color civilianColor = Color.blue;
    [SerializeField] private Color playerColor = Color.green;
    
    [Header("Performance")]
    [SerializeField] private int maxVisualElements = 50;
    [SerializeField] private float updateInterval = 0.1f;
    
    // Visual elements
    private Dictionary<string, LineRenderer> activeLines = new Dictionary<string, LineRenderer>();
    private Dictionary<string, GameObject> debugObjects = new Dictionary<string, GameObject>();
    private List<DebugVisualizationData> currentData = new List<DebugVisualizationData>();
    
    // GUI
    private Rect guiRect = new Rect(10, 10, 300, 600);
    private Vector2 scrollPosition = Vector2.zero;
    private bool showDetailedInfo = false;
    
    // Update control
    private float lastUpdateTime;
    
    void Update()
    {
        if (!enableVisualization) return;
        
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateVisualization();
            lastUpdateTime = Time.time;
        }
    }
    
    private void UpdateVisualization()
    {
        ClearOldVisualizations();
        GatherVisualizationData();
        RenderVisualizations();
    }
    
    private void GatherVisualizationData()
    {
        currentData.Clear();
        
        switch (debugMode)
        {
            case AIDebugMode.Overview:
                GatherOverviewData();
                break;
            case AIDebugMode.Guards:
                GatherGuardData();
                break;
            case AIDebugMode.Civilians:
                GatherCivilianData();
                break;
            case AIDebugMode.LineOfSight:
                GatherLineOfSightData();
                break;
            case AIDebugMode.Steering:
                GatherSteeringData();
                break;
            case AIDebugMode.Blackboard:
                GatherBlackboardData();
                break;
        }
    }
    
    private void GatherOverviewData()
    {
        // Guard overview
        var guards = FindObjectsOfType<Guard>();
        foreach (var guard in guards)
        {
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.Guard,
                position = guard.transform.position,
                targetPosition = guard.LastKnownPlayerPosition,
                state = guard.GetCurrentState().ToString(),
                additionalInfo = $"Health: {guard.Health:F0}/{guard.MaxHealth:F0}",
                color = guardColor,
                entityName = guard.name
            });
        }
        
        // Civilian overview
        var civilians = FindObjectsOfType<CivilianAI>();
        foreach (var civilian in civilians)
        {
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.Civilian,
                position = civilian.transform.position,
                targetPosition = civilian.GetPlayerPosition(),
                state = civilian.GetCurrentState().ToString(),
                additionalInfo = $"Panic: {civilian.GetPanicLevel():F0}%",
                color = civilianColor,
                entityName = civilian.name
            });
        }
    }
    
    private void GatherGuardData()
    {
        var guards = FindObjectsOfType<Guard>();
        foreach (var guard in guards)
        {
            var personalityController = guard.GetPersonalityController();
            string personalityInfo = personalityController != null ? 
                personalityController.GetPersonalityName() : "None";
            
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.Guard,
                position = guard.transform.position,
                targetPosition = guard.LastKnownPlayerPosition,
                state = guard.GetCurrentState().ToString(),
                additionalInfo = $"Personality: {personalityInfo}\n" +
                               $"Health: {guard.Health:F0}/{guard.MaxHealth:F0}\n" +
                               $"Can See Player: {guard.CanSeePlayer}",
                color = guardColor,
                entityName = guard.name
            });
            
            // Add detection range visualization
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.DetectionRange,
                position = guard.transform.position,
                range = guard.detectionRange,
                color = Color.yellow,
                entityName = $"{guard.name}_DetectionRange"
            });
        }
    }
    
    private void GatherCivilianData()
    {
        var civilians = FindObjectsOfType<CivilianAI>();
        foreach (var civilian in civilians)
        {
            var detectionLevel = civilian.GetDetectionLevel().ToString();
            
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.Civilian,
                position = civilian.transform.position,
                targetPosition = civilian.GetPlayerPosition(),
                state = civilian.GetCurrentState().ToString(),
                additionalInfo = $"Detection: {detectionLevel}\n" +
                               $"Panic: {civilian.GetPanicLevel():F0}%\n" +
                               $"Suspicious Areas: {civilian.GetSuspiciousPositionsCount()}",
                color = GetCivilianStateColor(civilian.GetCurrentState()),
                entityName = civilian.name
            });
        }
    }
    
    private void GatherLineOfSightData()
    {
        var playerDetectors = FindObjectsOfType<PlayerDetector>();
        var playerTransform = FindObjectOfType<PlayerController>()?.transform;
        
        if (playerTransform == null) return;
        
        foreach (var detector in playerDetectors)
        {
            bool canSee = detector.CanSeePlayer(playerTransform);
            
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.LineOfSight,
                position = detector.transform.position,
                targetPosition = playerTransform.position,
                state = canSee ? "CAN_SEE" : "CANNOT_SEE",
                color = canSee ? Color.green : Color.red,
                entityName = $"{detector.name}_LOS"
            });
            
            // Add field of view visualization
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.FieldOfView,
                position = detector.transform.position,
                direction = detector.transform.forward,
                range = detector.GetDetectionRange(),
                fieldOfView = detector.GetFieldOfView(),
                color = canSee ? Color.green * 0.3f : Color.yellow * 0.3f,
                entityName = $"{detector.name}_FOV"
            });
        }
    }
    
    private void GatherSteeringData()
    {
        var controllers = FindObjectsOfType<AIMovementController>();
        
        foreach (var controller in controllers)
        {
            var activeSteeringForces = controller.GetActiveSteeringForces();
            
            foreach (var force in activeSteeringForces)
            {
                currentData.Add(new DebugVisualizationData
                {
                    type = DebugElementType.SteeringForce,
                    position = controller.transform.position,
                    direction = force.direction,
                    magnitude = force.magnitude,
                    state = force.behaviorName,
                    color = GetSteeringForceColor(force.behaviorName),
                    entityName = $"{controller.name}_{force.behaviorName}"
                });
            }
        }
    }
    
    private void GatherBlackboardData()
    {
        var blackboard = ServiceLocator.Get<IBlackboard>();
        if (blackboard == null) return;
        
        // Visualize key blackboard data
        var alertPosition = blackboard.GetValue<Vector3>(BlackboardKeys.ALERT_POSITION);
        if (alertPosition != Vector3.zero)
        {
            currentData.Add(new DebugVisualizationData
            {
                type = DebugElementType.BlackboardData,
                position = alertPosition,
                state = "ALERT_POSITION",
                color = Color.orange,
                entityName = "BB_AlertPosition"
            });
        }
        
        var panicAreas = blackboard.GetValue<List<Vector3>>(BlackboardKeys.CIVILIAN_PANIC_AREAS);
        if (panicAreas != null)
        {
            for (int i = 0; i < panicAreas.Count; i++)
            {
                currentData.Add(new DebugVisualizationData
                {
                    type = DebugElementType.BlackboardData,
                    position = panicAreas[i],
                    state = "PANIC_AREA",
                    color = Color.magenta,
                    entityName = $"BB_PanicArea_{i}"
                });
            }
        }
    }
    
    private void RenderVisualizations()
    {
        foreach (var data in currentData)
        {
            RenderVisualizationElement(data);
        }
    }
    
    private void RenderVisualizationElement(DebugVisualizationData data)
    {
        switch (data.type)
        {
            case DebugElementType.Guard:
            case DebugElementType.Civilian:
                RenderEntityVisualization(data);
                break;
                
            case DebugElementType.LineOfSight:
                RenderLineOfSightVisualization(data);
                break;
                
            case DebugElementType.DetectionRange:
                RenderRangeVisualization(data);
                break;
                
            case DebugElementType.FieldOfView:
                RenderFieldOfViewVisualization(data);
                break;
                
            case DebugElementType.SteeringForce:
                RenderSteeringForceVisualization(data);
                break;
                
            case DebugElementType.BlackboardData:
                RenderBlackboardDataVisualization(data);
                break;
        }
    }
    
    private void RenderEntityVisualization(DebugVisualizationData data)
    {
        // Draw connection line to target if exists
        if (data.targetPosition != Vector3.zero && data.targetPosition != data.position)
        {
            CreateOrUpdateLine($"{data.entityName}_TargetLine", data.position, data.targetPosition, data.color);
        }
        
        // Create floating text for state information
        CreateOrUpdateFloatingText(data.entityName, data.position + Vector3.up * 2f, $"{data.state}\n{data.additionalInfo}");
    }
    
    private void RenderLineOfSightVisualization(DebugVisualizationData data)
    {
        CreateOrUpdateLine(data.entityName, data.position, data.targetPosition, data.color);
    }
    
    private void RenderRangeVisualization(DebugVisualizationData data)
    {
        // This would be rendered in OnDrawGizmos for wireframes
        // Or create a circle mesh for runtime visualization
    }
    
    private void RenderFieldOfViewVisualization(DebugVisualizationData data)
    {
        // Create FOV arc visualization
        CreateOrUpdateFieldOfViewMesh(data.entityName, data.position, data.direction, data.range, data.fieldOfView, data.color);
    }
    
    private void RenderSteeringForceVisualization(DebugVisualizationData data)
    {
        Vector3 endPosition = data.position + data.direction * data.magnitude;
        CreateOrUpdateLine(data.entityName, data.position, endPosition, data.color);
        
        // Add arrow head
        CreateArrowHead(data.entityName + "_Arrow", endPosition, data.direction, data.color);
    }
    
    private void RenderBlackboardDataVisualization(DebugVisualizationData data)
    {
        CreateOrUpdateFloatingText(data.entityName, data.position + Vector3.up, data.state);
    }
    
    // Helper methods for creating visual elements
    private void CreateOrUpdateLine(string name, Vector3 start, Vector3 end, Color color)
    {
        if (!activeLines.ContainsKey(name))
        {
            GameObject lineObj = new GameObject($"DebugLine_{name}");
            lineObj.transform.SetParent(transform);
            
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = lineMaterial ?? CreateDefaultLineMaterial();
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.positionCount = 2;
            
            activeLines[name] = lr;
        }
        
        var lineRenderer = activeLines[name];
        lineRenderer.color = color;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        lineRenderer.gameObject.SetActive(true);
    }
    
    private void CreateOrUpdateFloatingText(string name, Vector3 position, string text)
    {
        // This would require a UI system or 3D text mesh
        // For simplicity, using Debug.DrawRay with info
        Debug.DrawRay(position, Vector3.up * 0.5f, Color.white);
    }
    
    private void CreateOrUpdateFieldOfViewMesh(string name, Vector3 position, Vector3 direction, float range, float fov, Color color)
    {
        // Complex mesh creation for FOV visualization
        // This would create a cone or arc mesh
    }
    
    private void CreateArrowHead(string name, Vector3 position, Vector3 direction, Color color)
    {
        float arrowSize = 0.3f;
        Vector3 right = Vector3.Cross(direction, Vector3.up) * arrowSize;
        Vector3 backward = -direction * arrowSize;
        
        CreateOrUpdateLine(name + "_R", position, position + backward + right, color);
        CreateOrUpdateLine(name + "_L", position, position + backward - right, color);
    }
    
    private Material CreateDefaultLineMaterial()
    {
        var material = new Material(Shader.Find("Sprites/Default"));
        return material;
    }
    
    private void ClearOldVisualizations()
    {
        // Hide all active visual elements
        foreach (var line in activeLines.Values)
        {
            if (line != null)
                line.gameObject.SetActive(false);
        }
        
        foreach (var obj in debugObjects.Values)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }
    
    private Color GetCivilianStateColor(CivilianState state)
    {
        switch (state)
        {
            case CivilianState.Normal: return Color.white;
            case CivilianState.Suspicious: return Color.yellow;
            case CivilianState.Panicking: return Color.orange;
            case CivilianState.Fleeing: return Color.red;
            case CivilianState.Alerting: return Color.cyan;
            default: return Color.gray;
        }
    }
    
    private Color GetSteeringForceColor(string behaviorName)
    {
        switch (behaviorName)
        {
            case "Seek": return Color.green;
            case "Flee": return Color.red;
            case "Pursuit": return Color.blue;
            case "Evade": return Color.magenta;
            case "ObstacleAvoidance": return Color.orange;
            default: return Color.white;
        }
    }
    
    void OnGUI()
    {
        if (!enableVisualization || !showGUI) return;
        
        GUI.backgroundColor = Color.black * 0.8f;
        guiRect = GUI.Window(0, guiRect, DrawDebugWindow, "AI Debug Visualizer");
    }
    
    private void DrawDebugWindow(int windowID)
    {
        GUILayout.BeginVertical();
        
        // Mode selection
        debugMode = (AIDebugMode)GUILayout.SelectionGrid((int)debugMode, System.Enum.GetNames(typeof(AIDebugMode)), 2);
        
        GUILayout.Space(10);
        
        // Controls
        showDetailedInfo = GUILayout.Toggle(showDetailedInfo, "Show Detailed Info");
        showGizmos = GUILayout.Toggle(showGizmos, "Show Gizmos");
        
        GUILayout.Space(10);
        
        // Statistics
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
        
        GUILayout.Label($"Active Elements: {currentData.Count}");
        GUILayout.Label($"Update Interval: {updateInterval:F2}s");
        
        if (showDetailedInfo)
        {
            foreach (var data in currentData.Take(10)) // Limit to first 10 for performance
            {
                GUILayout.Label($"{data.entityName}: {data.state}");
                if (!string.IsNullOrEmpty(data.additionalInfo))
                {
                    GUILayout.Label($"  {data.additionalInfo}", GUI.skin.box);
                }
            }
        }
        
        GUILayout.EndScrollView();
        
        GUILayout.Space(10);
        
        // Quick test buttons
        if (GUILayout.Button("Run Quick Test"))
        {
            var testSuite = FindObjectOfType<AITestSuite>();
            if (testSuite != null)
                testSuite.ValidateTPRequirements();
        }
        
        if (GUILayout.Button("Clear Visualizations"))
        {
            ClearOldVisualizations();
            currentData.Clear();
        }
        
        GUILayout.EndVertical();
        
        GUI.DragWindow();
    }
    
    void OnDrawGizmos()
    {
        if (!enableVisualization || !showGizmos) return;
        
        foreach (var data in currentData)
        {
            Gizmos.color = data.color;
            
            switch (data.type)
            {
                case DebugElementType.DetectionRange:
                    Gizmos.DrawWireSphere(data.position, data.range);
                    break;
                    
                case DebugElementType.Guard:
                case DebugElementType.Civilian:
                    Gizmos.DrawWireCube(data.position + Vector3.up * 2f, Vector3.one * 0.5f);
                    break;
                    
                case DebugElementType.BlackboardData:
                    Gizmos.DrawWireCube(data.position, Vector3.one);
                    break;
            }
        }
    }
}

// Supporting enums and structures
public enum AIDebugMode
{
    Overview,
    Guards,
    Civilians,
    LineOfSight,
    Steering,
    Blackboard
}

public enum DebugElementType
{
    Guard,
    Civilian,
    LineOfSight,
    DetectionRange,
    FieldOfView,
    SteeringForce,
    BlackboardData
}

[System.Serializable]
public struct DebugVisualizationData
{
    public DebugElementType type;
    public Vector3 position;
    public Vector3 targetPosition;
    public Vector3 direction;
    public float range;
    public float fieldOfView;
    public float magnitude;
    public string state;
    public string additionalInfo;
    public Color color;
    public string entityName;
}
```

---

## ✅ **CRITERIOS DE COMPLETITUD**

Al finalizar esta fase deberás tener:

1. **✅ Test Suite automatizado** validando todos los sistemas
2. **✅ Herramientas de debugging visual** en tiempo real
3. **✅ Validación de requisitos TP** automatizada
4. **✅ Métricas de rendimiento** y estadísticas
5. **✅ Reportes detallados** de funcionamiento
6. **✅ Continuous validation** para detección temprana de problemas
7. **✅ Demo modes** para presentación del TP

### **Testing:**
1. **Automated Testing**: Suite debe ejecutar sin errores y validar todos los sistemas
2. **Visual Debugging**: Herramientas deben mostrar claramente el estado de AI
3. **TP Validation**: Debe confirmar que todos los requisitos están cumplidos
4. **Performance**: Sistema debe mantener framerate estable
5. **Error Detection**: Debe detectar y reportar problemas automáticamente

Esta fase asegura que tu implementación esté **completamente validada** y **lista para presentar**, con herramientas profesionales de debugging y testing que demuestran la calidad del trabajo.