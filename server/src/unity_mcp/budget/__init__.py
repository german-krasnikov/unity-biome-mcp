"""Cost budgeting + adaptive routing for Haiku calls.

Enable: UNITY_MCP_HAIKU_BUDGET=0.50 (USD per session, default $0.50)
        UNITY_MCP_HAIKU_DAY_CAP=5.00 (USD per day, default $5)
        UNITY_MCP_BUDGET_DISABLED=1 (bypass, dev mode)
"""
from .cost_tracker import CostTracker
from .registry import FEATURES, FeatureMeta, get_feature
from .router import BudgetRouter, RouteDecision

__all__ = ["FEATURES", "FeatureMeta", "get_feature", "CostTracker", "BudgetRouter", "RouteDecision"]
