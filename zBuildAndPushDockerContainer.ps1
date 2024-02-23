powershell -Command {

# Define the tag name for your Docker image
$tagName = "api-market-catalyst-black:latest"

Write-Host "Building Docker image $tagName"

# Build the Docker container
docker build -t $tagName .

# Get the AWS account number dynamically
$awsAccount = aws --profile terraform sts get-caller-identity --query "Account" --output text --profile terraform

# Construct the ECR repository URL
$ecrRepoUrl = "$($awsAccount).dkr.ecr.us-east-1.amazonaws.com"

# Log in to ECR
aws ecr get-login-password --profile terraform | docker login --username AWS --password-stdin $ecrRepoUrl

# Tag the Docker image
docker tag api-market-catalyst-black:latest $ecrRepoUrl/api-market-catalyst-black:latest

# Push the Docker image to ECR
docker push $ecrRepoUrl/api-market-catalyst-black:latest

}