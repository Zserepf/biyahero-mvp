#!/usr/bin/env bash
# Bootstrap the AWS CDK environment for BiyaHero.
# Targets us-east-1 (free-tier-eligible region).
#
# Prerequisites:
#   - AWS CLI configured with valid credentials
#   - Node.js installed (for CDK CLI)
#   - .NET 8 SDK installed
#
# Usage:
#   chmod +x bootstrap.sh
#   ./bootstrap.sh

set -euo pipefail

REGION="us-east-1"

echo "==> Bootstrapping CDK in region: ${REGION}"
npx cdk bootstrap "aws://$(aws sts get-caller-identity --query Account --output text)/${REGION}"

echo "==> CDK bootstrap complete for ${REGION}."
