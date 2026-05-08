-- =====================================================
-- DATABASE
-- =====================================================
CREATE DATABASE IF NOT EXISTS task_management;
USE task_management;

-- =====================================================
-- USERS
-- =====================================================
CREATE TABLE users (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    mobile VARCHAR(20) UNIQUE,
    email VARCHAR(150) UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role ENUM('ADMIN', 'AGENT') NOT NULL,
    status ENUM('ACTIVE', 'INACTIVE') DEFAULT 'ACTIVE',
    is_deleted TINYINT(1) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- =====================================================
-- USER SESSIONS
-- =====================================================
CREATE TABLE user_sessions (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    token VARCHAR(500) NOT NULL,
    device_info VARCHAR(255),
    expires_at DATETIME,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_token (user_id, token)
);

-- =====================================================
-- AGENT PROFILE
-- =====================================================
CREATE TABLE agent_profiles (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT NOT NULL,
    address TEXT,
    joining_date DATE,
    
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- =====================================================
-- EXCEL UPLOAD TRACKING
-- =====================================================
CREATE TABLE excel_uploads (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    file_name VARCHAR(255),
    file_path VARCHAR(500),
    uploaded_by BIGINT,
    total_rows INT DEFAULT 0,
    success_rows INT DEFAULT 0,
    failed_rows INT DEFAULT 0,
    status ENUM('PROCESSING', 'COMPLETED', 'FAILED') DEFAULT 'PROCESSING',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (uploaded_by) REFERENCES users(id)
);

-- =====================================================
-- EXCEL UPLOAD ERRORS (FIXED)
-- =====================================================
CREATE TABLE excel_upload_errors (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    
    upload_id BIGINT,
    excel_row_number INT,
    
    error_message TEXT,
    raw_data JSON,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (upload_id) REFERENCES excel_uploads(id) ON DELETE CASCADE
);

-- =====================================================
-- TASKS
-- =====================================================
CREATE TABLE tasks (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    
    internal_id VARCHAR(50) NOT NULL UNIQUE,
    application_no VARCHAR(100),
    
    customer_name VARCHAR(150),
    customer_mobile VARCHAR(20),
    customer_address TEXT,
    
    assigned_agent_id BIGINT,
    assigned_date DATE,
    due_date DATE,
    
    status ENUM('OPEN', 'IN_PROGRESS', 'CLOSED') DEFAULT 'OPEN',
    
    last_update_id BIGINT NULL,
    
    acknowledged TINYINT(1) DEFAULT 0,
    acknowledged_at DATETIME NULL,
    
    raw_data JSON,
    
    created_from_upload_id BIGINT,
    
    is_deleted TINYINT(1) DEFAULT 0,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (assigned_agent_id) REFERENCES users(id),
    FOREIGN KEY (created_from_upload_id) REFERENCES excel_uploads(id),
    
    INDEX idx_agent (assigned_agent_id),
    INDEX idx_status (status),
    INDEX idx_app_no (application_no),
    INDEX idx_agent_status_date (assigned_agent_id, status, assigned_date)
);

-- =====================================================
-- TASK ASSIGNMENTS
-- =====================================================
CREATE TABLE task_assignments (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    task_id BIGINT NOT NULL,
    agent_id BIGINT NOT NULL,
    assigned_by BIGINT,
    assigned_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (agent_id) REFERENCES users(id),
    FOREIGN KEY (assigned_by) REFERENCES users(id),
    
    INDEX idx_task (task_id)
);

-- =====================================================
-- TASK ACKNOWLEDGEMENTS
-- =====================================================
CREATE TABLE task_acknowledgements (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    task_id BIGINT,
    agent_id BIGINT,
    acknowledged_at DATETIME,
    
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (agent_id) REFERENCES users(id),
    
    INDEX idx_task_agent (task_id, agent_id)
);

-- =====================================================
-- TASK UPDATES
-- =====================================================
CREATE TABLE task_updates (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    
    task_id BIGINT NOT NULL,
    agent_id BIGINT NOT NULL,
    
    status ENUM(
        'PENDING',
        'VISITED',
        'NOT_INTERESTED',
        'CONVERTED',
        'FOLLOW_UP_REQUIRED'
    ) NOT NULL,
    
    comment TEXT,
    
    meeting_person_name VARCHAR(150),
    meeting_person_mobile VARCHAR(20),
    
    followup_date DATE,
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (agent_id) REFERENCES users(id),
    
    INDEX idx_task (task_id),
    INDEX idx_agent (agent_id),
    INDEX idx_task_created (task_id, created_at)
);

-- =====================================================
-- TASK STATUS HISTORY
-- =====================================================
CREATE TABLE task_status_history (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    task_id BIGINT,
    old_status VARCHAR(50),
    new_status VARCHAR(50),
    changed_by BIGINT,
    changed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (task_id) REFERENCES tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (changed_by) REFERENCES users(id)
);

-- =====================================================
-- REPORT EXPORTS
-- =====================================================
CREATE TABLE report_exports (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    file_name VARCHAR(255),
    file_path VARCHAR(500),
    generated_by BIGINT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (generated_by) REFERENCES users(id)
);

-- =====================================================
-- REPORT CACHE
-- =====================================================
CREATE TABLE report_cache (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    filter_params JSON,
    result_data JSON,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- NOTIFICATIONS
-- =====================================================
CREATE TABLE notifications (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT,
    title VARCHAR(255),
    message TEXT,
    is_read TINYINT(1) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (user_id) REFERENCES users(id)
);

-- =====================================================
-- AUDIT LOGS
-- =====================================================
CREATE TABLE audit_logs (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id BIGINT,
    action VARCHAR(100),
    entity_type VARCHAR(50),
    entity_id BIGINT,
    old_value JSON,
    new_value JSON,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (user_id) REFERENCES users(id)
);

-- =====================================================
-- TRIGGER (FIXED DELIMITER)
-- =====================================================
DELIMITER $$

CREATE TRIGGER trg_after_task_update
AFTER INSERT ON task_updates
FOR EACH ROW
BEGIN
    UPDATE tasks 
    SET last_update_id = NEW.id,
        status = 
            CASE 
                WHEN NEW.status = 'CONVERTED' THEN 'CLOSED'
                ELSE 'IN_PROGRESS'
            END
    WHERE id = NEW.task_id;
END$$

DELIMITER ;

-- =====================================================
-- SAMPLE DATA
-- =====================================================
INSERT INTO users (name, mobile, email, password_hash, role)
VALUES ('Admin', '9999999999', 'admin@test.com', 'hashed_password', 'ADMIN');

INSERT INTO users (name, mobile, email, password_hash, role)
VALUES ('Agent One', '8888888888', 'agent@test.com', 'hashed_password', 'AGENT');