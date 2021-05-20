function intersect = InterX(lines)
warning('off')
% Check for NaN
if(sum(isnan(lines))>0)
    intersect = 0;
    return
end
% Sample data
L1_x1 = lines(1,1);
L1_y1 = lines(1,2);
L1_x2 = lines(2,1);
L1_y2 = lines(2,2);
L2_x1 = lines(3,1);
L2_y1 = lines(3,2);
L2_x2 = lines(4,1);
L2_y2 = lines(4,2);

% Compute several intermediate quantities
Dx12 = L1_x1-L1_x2;
Dx34 = L2_x1-L2_x2;
Dy12 = L1_y1-L1_y2;
Dy34 = L2_y1-L2_y2;
Dx24 = L1_x2-L2_x2;
Dy24 = L1_y2-L2_y2;
% Solve for t and s parameters
ts = [Dx12 -Dx34; Dy12 -Dy34] \ [-Dx24; -Dy24];

% Take weighted combinations of points on the line
P = ts(1)*[L1_x1; L1_y1] + (1-ts(1))*[L1_x2; L1_y2];
Q = ts(2)*[L2_x1; L2_y1] + (1-ts(2))*[L2_x2; L2_y2];

intersect = 0;
if(L2_y1 > L1_y1)
    if(P(2) < L2_y1 && P(2) > L1_y1 && P(1) > L1_x1 && P(1)~=L2_x1 && P(2) ~= L2_y1)
        intersect = 1;
    end
elseif(L2_y1 < L1_y1)
    if( P(1)~=L2_x1 && P(2) ~= L2_y1 && P(1) > L1_x1 && P(2) < L1_y1 && P(2) < L2_y1)        
        intersect = 1;
    end
end

if(intersect == 1 && false) % Debug
    % Plot the lines
    figure
    plot([L1_x1 L1_x2], [L1_y1 L1_y2],'r') % Pedestrian
    hold on
    plot(L1_x1,L1_y1,'ro')
    plot([L2_x1 L2_x2], [L2_y1 L2_y2],'b') % Driver
    plot(L2_x1,L2_y1,'bo')
    xlabel('x');
    ylabel('z');
    % Plot intersection points
    plot(P(1), P(2), 'ko')
    plot(Q(1), Q(2), 'co')
    hold off
end

warning('on')
end