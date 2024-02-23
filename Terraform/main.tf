provider "aws" {
  region  = "us-east-1"
  profile = "terraform"
}

resource "aws_ecr_repository" "api_market_catalyst_black" {
  name                 = "api-market-catalyst-black"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }
}

# ALB Definition

resource "aws_vpc" "catalyst_black" {
  tags = {
    "Name" = "CatalystBlack-vpc"
  }
}

resource "aws_subnet" "public_01" {
  vpc_id            = aws_vpc.catalyst_black.id
  cidr_block        = "10.0.0.0/20"
  availability_zone = "us-east-1a"

  tags = {
    "Name" = "CatalystBlack-subnet-public1-us-east-1a"
  }
}

resource "aws_subnet" "public_02" {
  vpc_id            = aws_vpc.catalyst_black.id
  cidr_block        = "10.0.16.0/20"
  availability_zone = "us-east-1b"
  
  tags = {
    "Name" = "CatalystBlack-subnet-public2-us-east-1b"
  }
}

resource "aws_instance" "server_01" {
  ami           = "ami-0c7217cdde317cfec"
  instance_type = "t2.small"
}

resource "aws_route53_zone" "catalyst_black" {
  name = "catalyst.black"
}

resource "aws_acm_certificate" "cert" {
  domain_name       = "api.market.catalyst.black"
  validation_method = "DNS"
}

resource "aws_security_group" "alb_sg" {
  name        = "alb-security-group"
  description = "Security group for ALB"
  vpc_id      = aws_vpc.catalyst_black.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_route53_record" "cert_validation" {
  for_each = {
    for dvo in toset(aws_acm_certificate.cert.domain_validation_options) : dvo.domain_name => {
      name   = dvo.resource_record_name
      type   = dvo.resource_record_type
      value  = dvo.resource_record_value
    }
  }

  zone_id = aws_route53_zone.catalyst_black.id
  name    = each.value.name
  type    = each.value.type
  records = [each.value.value]
  ttl     = 60
}

resource "aws_acm_certificate_validation" "cert" {
  certificate_arn         = aws_acm_certificate.cert.arn
  validation_record_fqdns = [for _, record in aws_route53_record.cert_validation : record.fqdn]
}

resource "aws_lb" "alb" {
  name               = "api-market-catalyst-black-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb_sg.id]
  subnets            = [aws_subnet.public_01.id, aws_subnet.public_02.id]

  enable_deletion_protection = false
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.alb.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"
    redirect {
      protocol   = "HTTPS"
      port       = "443"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_target_group" "ec2_tg" {
  name     = "api-market-catalyst-black-tg"
  port     = 80
  protocol = "HTTP"
  vpc_id   = aws_vpc.catalyst_black.id

  health_check {
    enabled             = true
    healthy_threshold   = 3
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
    path                = "/"
    protocol            = "HTTP"
    matcher             = "200"
  }

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_lb_target_group_attachment" "ec2_instance_attachment" {
  target_group_arn = aws_lb_target_group.ec2_tg.arn
  target_id        = aws_instance.server_01.id
  port             = 8080
}


resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.alb.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-2016-08"
  certificate_arn   = aws_acm_certificate.cert.arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.ec2_tg.arn
  }
}

resource "aws_route53_record" "api_market_catalyst_black" {
  zone_id = aws_route53_zone.catalyst_black.zone_id
  name    = "api.market"
  type    = "A"

  alias {
    name                   = aws_lb.alb.dns_name
    zone_id                = aws_lb.alb.zone_id
    evaluate_target_health = true
  }
}